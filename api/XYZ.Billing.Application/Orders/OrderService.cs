using XYZ.Billing.Application.Exceptions;
using XYZ.Billing.Application.Payments;
using XYZ.Billing.Domain.Entities;
using XYZ.Billing.Domain.Enums;
using XYZ.Billing.Domain.Repositories;

namespace XYZ.Billing.Application.Orders;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IReadOnlyDictionary<string, IPaymentGateway> _paymentGateways;
    
    public OrderService(IOrderRepository orderRepository, IEnumerable<IPaymentGateway> paymentGateways)
    {
        _orderRepository = orderRepository;
        _paymentGateways = paymentGateways.ToDictionary(gateway => gateway.Id, StringComparer.OrdinalIgnoreCase);
    }
    
    public async Task<OrderSubmissionResult> SubmitOrderAsync(OrderRequest orderRequest, CancellationToken ct = default)
    {
        if (!_paymentGateways.TryGetValue(orderRequest.PaymentGatewayId, out IPaymentGateway? paymentGateway))
        {
            throw new UnsupportedPaymentGatewayException(orderRequest.PaymentGatewayId);
        }
        
        Order? existingOrder = await _orderRepository.GetSingleAsync(o => o.OrderNumber == orderRequest.OrderNumber, ct);
        if (existingOrder != null)
        {
            return await HandleExistingOrder(existingOrder, orderRequest, paymentGateway, ct);
        }

        Order order = new Order
        {
            OrderNumber = orderRequest.OrderNumber,
            UserId = orderRequest.UserId,
            Amount = orderRequest.Amount,
            PaymentGatewayId = orderRequest.PaymentGatewayId,
            Description = orderRequest.Description,
            Status = OrderPaymentStatus.Processing,
            CreatedAt = DateTimeOffset.UtcNow
        };
        
        OrderCreationResult creationResult = await _orderRepository.TryAddAsync(order, ct);
        if (creationResult.Created == false)
        {
            return await HandleExistingOrder(creationResult.Order, orderRequest, paymentGateway, ct);
        }

        return await ProcessPayment(order, orderRequest, paymentGateway, ct);
    }

    private async Task<OrderSubmissionResult> ProcessPayment(
        Order order,
        OrderRequest orderRequest,
        IPaymentGateway paymentGateway,
        CancellationToken ct)
    {
        PaymentResult paymentResult = await paymentGateway.ProcessPaymentAsync(orderRequest);
        if (paymentResult.IsSuccessful)
        {
            order.Status = OrderPaymentStatus.Paid;
            order.PaidAt = DateTimeOffset.UtcNow;
            order.Confirmation = paymentResult.Confirmation ?? throw new InvalidOperationException($"Payment gateway '{paymentGateway.Id}' returned a successful payment result without a confirmation.");
        }
        else
        {
            order.Status = OrderPaymentStatus.Declined;
            order.FailureReason = paymentResult.FailureReason ?? "Payment was declined.";
        }

        _orderRepository.Update(order, ct);
        await _orderRepository.SaveChangesAsync(ct);
        return ToOrderSubmissionResult(order, isDuplicate: false);
    }

    public async Task<PaymentReceipt> GetPaymentReceiptAsync(string orderNumber, CancellationToken ct = default)
    {
        Order? order = await _orderRepository.GetSingleAsync(o => o.OrderNumber == orderNumber, ct);
        if (order == null)
        {
            throw new OrderNotFoundException();
        }

        if (order.Status != OrderPaymentStatus.Paid)
        {
            throw new ReceiptNotFoundException();
        }

        return CreateReceipt(order);
    }
    
    private async Task<OrderSubmissionResult> HandleExistingOrder(
        Order existing,
        OrderRequest request,
        IPaymentGateway paymentGateway,
        CancellationToken ct)
    {
        if (!HasSameOrderDetails(existing, request))
        {
            throw new OrderNumberConflictException();
        }

        if (existing.Status != OrderPaymentStatus.Declined ||
            string.Equals(existing.PaymentGatewayId, request.PaymentGatewayId, StringComparison.OrdinalIgnoreCase))
        {
            return ToOrderSubmissionResult(existing, isDuplicate: true);
        }

        bool retryClaimed = await _orderRepository.TryBeginRetryAsync(existing, request.PaymentGatewayId, ct);
        if (!retryClaimed)
        {
            _orderRepository.DetachEntity(existing);
            Order? currentOrder = await _orderRepository.GetSingleAsync(
                order => order.OrderNumber == request.OrderNumber, ct);
            if (currentOrder == null)
            {
                throw new OrderNotFoundException();
            }

            return ExistingOrderSubmission(currentOrder, request);
        }

        existing.Status = OrderPaymentStatus.Processing;
        existing.PaymentGatewayId = request.PaymentGatewayId;
        existing.FailureReason = null;
        existing.Confirmation = null;
        existing.PaidAt = null;
        return await ProcessPayment(existing, request, paymentGateway, ct);
    }

    private static OrderSubmissionResult ExistingOrderSubmission(Order existing, OrderRequest request)
    {
        if (!HasSameOrderDetails(existing, request))
        {
            throw new OrderNumberConflictException();
        }

        return ToOrderSubmissionResult(existing, isDuplicate: true);
    }

    private static bool HasSameOrderDetails(Order existing, OrderRequest request)
    {
        return existing.UserId == request.UserId &&
           existing.Amount == request.Amount &&
           existing.Description == request.Description;
    }

    private static OrderSubmissionResult ToOrderSubmissionResult(Order order, bool isDuplicate)
    {
        return order.Status switch
        {
            OrderPaymentStatus.Paid => new OrderSubmissionResult(CreateReceipt(order), isDuplicate),
            OrderPaymentStatus.Declined => throw new PaymentDeclinedException(order.FailureReason ?? "Payment was declined."),
            OrderPaymentStatus.Processing => throw new OrderProcessingException(),
            _ => throw new InvalidOperationException($"Order '{order.OrderNumber}' is in an unexpected state '{order.Status}'.")
        };
    }

    private static PaymentReceipt CreateReceipt(Order order)
    {
        return new PaymentReceipt(
            order.OrderNumber,
            order.Amount,
            order.PaidAt ?? throw new InvalidOperationException($"Paid order '{order.OrderNumber}' has no payment timestamp."),
            order.Confirmation ?? throw new InvalidOperationException($"Paid order '{order.OrderNumber}' has no payment confirmation.")
        );
    }
}