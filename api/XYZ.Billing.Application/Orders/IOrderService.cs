namespace XYZ.Billing.Application.Orders;

public interface IOrderService
{
    Task<OrderSubmissionResult> SubmitOrderAsync(OrderRequest orderRequest, CancellationToken ct = default);

    Task<PaymentReceipt> GetPaymentReceiptAsync(string orderNumber, CancellationToken ct = default);
}