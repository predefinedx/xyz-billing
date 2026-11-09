using System.Linq.Expressions;
using Moq;
using Shouldly;
using XYZ.Billing.Application.Exceptions;
using XYZ.Billing.Application.Orders;
using XYZ.Billing.Application.Payments;
using XYZ.Billing.Domain.Entities;
using XYZ.Billing.Domain.Enums;
using XYZ.Billing.Domain.Repositories;

namespace XYZ.Billing.Application.Tests;

public class OrderServiceTests
{
    private const string GatewayId = "mock";

    [Fact]
    public async Task SubmitOrderAsync_WhenPaymentSucceeds_ReturnsReceiptAndSavesPaidOrder()
    {
        Mock<IOrderRepository> repository = CreateRepositoryWithNoExistingOrder();
        Mock<IPaymentGateway> gateway = CreateGateway(
            new PaymentResult(true, "CONFIRM-123", null));
        OrderService service = CreateService(repository, gateway);

        OrderSubmissionResult result = await service.SubmitOrderAsync(CreateRequest());

        result.IsDuplicate.ShouldBeFalse();
        result.Receipt.OrderNumber.ShouldBe("ORDER-1");
        result.Receipt.Amount.ShouldBe(25.50m);
        result.Receipt.Confirmation.ShouldBe("CONFIRM-123");
        result.Receipt.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
        gateway.Verify(x => x.ProcessPaymentAsync(It.IsAny<OrderRequest>()), Times.Once);
        repository.Verify(x => x.Update(
            It.Is<Order>(order => order.Status == OrderPaymentStatus.Paid), It.IsAny<CancellationToken>()),
            Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenPaymentIsDeclined_ThrowsAndSavesDeclinedOrder()
    {
        Mock<IOrderRepository> repository = CreateRepositoryWithNoExistingOrder();
        Mock<IPaymentGateway> gateway = CreateGateway(
            new PaymentResult(false, null, "Card declined."));
        OrderService service = CreateService(repository, gateway);

        PaymentDeclinedException exception =
            await Should.ThrowAsync<PaymentDeclinedException>(() => service.SubmitOrderAsync(CreateRequest()));

        exception.Message.ShouldBe("Card declined.");
        gateway.Verify(x => x.ProcessPaymentAsync(It.IsAny<OrderRequest>()), Times.Once);
        repository.Verify(x => x.Update(
            It.Is<Order>(order => order.Status == OrderPaymentStatus.Declined &&
                                  order.FailureReason == "Card declined."),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenSameOrderAlreadyExists_ReturnsDuplicateWithoutChargingAgain()
    {
        Order existingOrder = CreateOrder(OrderPaymentStatus.Paid);
        Mock<IOrderRepository> repository = CreateRepositoryReturning(existingOrder);
        Mock<IPaymentGateway> gateway = CreateGateway(new PaymentResult(true, "unused", null));
        OrderService service = CreateService(repository, gateway);

        OrderSubmissionResult result = await service.SubmitOrderAsync(CreateRequest());

        result.IsDuplicate.ShouldBeTrue();
        result.Receipt.Confirmation.ShouldBe("CONFIRM-123");
        gateway.Verify(x => x.ProcessPaymentAsync(It.IsAny<OrderRequest>()), Times.Never);
        repository.Verify(x => x.TryAddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenOrderNumberIsReusedWithDifferentDetails_ThrowsConflict()
    {
        Order existingOrder = CreateOrder(OrderPaymentStatus.Paid);
        Mock<IOrderRepository> repository = CreateRepositoryReturning(existingOrder);
        Mock<IPaymentGateway> gateway = CreateGateway(new PaymentResult(true, "unused", null));
        OrderService service = CreateService(repository, gateway);
        OrderRequest request = new()
        {
            OrderNumber = "ORDER-1",
            UserId = "USER-1",
            Amount = 30m,
            PaymentGatewayId = GatewayId
        };

        await Should.ThrowAsync<OrderNumberConflictException>(() => service.SubmitOrderAsync(request));

        gateway.Verify(x => x.ProcessPaymentAsync(It.IsAny<OrderRequest>()), Times.Never);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenDeclinedOrderIsResubmittedWithDifferentGateway_RetriesPayment()
    {
        Order declinedOrder = CreateOrder(OrderPaymentStatus.Declined);
        declinedOrder.FailureReason = "Card declined.";
        Mock<IOrderRepository> repository = CreateRepositoryReturning(declinedOrder);
        repository.Setup(x => x.TryBeginRetryAsync(
                declinedOrder, "mock-success", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Mock<IPaymentGateway> gateway = CreateGateway(
            new PaymentResult(true, "CONFIRM-RETRY", null), "mock-success");
        OrderService service = CreateService(repository, gateway);
        OrderRequest request = CreateRequest("mock-success");

        OrderSubmissionResult result = await service.SubmitOrderAsync(request);

        result.IsDuplicate.ShouldBeFalse();
        result.Receipt.Confirmation.ShouldBe("CONFIRM-RETRY");
        result.Receipt.Amount.ShouldBe(declinedOrder.Amount);
        gateway.Verify(x => x.ProcessPaymentAsync(request), Times.Once);
        repository.Verify(x => x.TryBeginRetryAsync(
            declinedOrder, "mock-success", It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.Update(
            It.Is<Order>(order => order.Status == OrderPaymentStatus.Paid &&
                                  order.PaymentGatewayId == "mock-success" &&
                                  order.FailureReason == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenDeclinedOrderUsesSameGateway_DoesNotRetry()
    {
        Order declinedOrder = CreateOrder(OrderPaymentStatus.Declined);
        declinedOrder.FailureReason = "Card declined.";
        Mock<IOrderRepository> repository = CreateRepositoryReturning(declinedOrder);
        Mock<IPaymentGateway> gateway = CreateGateway(new PaymentResult(true, "unused", null));
        OrderService service = CreateService(repository, gateway);

        await Should.ThrowAsync<PaymentDeclinedException>(() => service.SubmitOrderAsync(CreateRequest()));

        repository.Verify(x => x.TryBeginRetryAsync(
            It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        gateway.Verify(x => x.ProcessPaymentAsync(It.IsAny<OrderRequest>()), Times.Never);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenOrderIsProcessing_DoesNotRetryThroughDifferentGateway()
    {
        Order processingOrder = CreateOrder(OrderPaymentStatus.Processing);
        Mock<IOrderRepository> repository = CreateRepositoryReturning(processingOrder);
        Mock<IPaymentGateway> gateway = CreateGateway(
            new PaymentResult(true, "unused", null), "mock-success");
        OrderService service = CreateService(repository, gateway);
        OrderRequest request = CreateRequest("mock-success");

        await Should.ThrowAsync<OrderProcessingException>(() => service.SubmitOrderAsync(request));

        repository.Verify(x => x.TryBeginRetryAsync(
            It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        gateway.Verify(x => x.ProcessPaymentAsync(It.IsAny<OrderRequest>()), Times.Never);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenRetryChangesOrderDetails_ThrowsConflictWithoutCharging()
    {
        Order declinedOrder = CreateOrder(OrderPaymentStatus.Declined);
        Mock<IOrderRepository> repository = CreateRepositoryReturning(declinedOrder);
        Mock<IPaymentGateway> gateway = CreateGateway(
            new PaymentResult(true, "unused", null), "mock-success");
        OrderService service = CreateService(repository, gateway);
        OrderRequest request = CreateRequest("mock-success", 30m);

        await Should.ThrowAsync<OrderNumberConflictException>(() => service.SubmitOrderAsync(request));

        repository.Verify(x => x.TryBeginRetryAsync(
            It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        gateway.Verify(x => x.ProcessPaymentAsync(It.IsAny<OrderRequest>()), Times.Never);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenRetryClaimWasLost_DoesNotChargeAgain()
    {
        Order declinedOrder = CreateOrder(OrderPaymentStatus.Declined);
        Order processingOrder = CreateOrder(OrderPaymentStatus.Processing);
        processingOrder.PaymentGatewayId = "mock-success";
        Mock<IOrderRepository> repository = new();
        repository.SetupSequence(x => x.GetSingleAsync(
                It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(declinedOrder)
            .ReturnsAsync(processingOrder);
        repository.Setup(x => x.TryBeginRetryAsync(
                declinedOrder, "mock-success", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock<IPaymentGateway> gateway = CreateGateway(
            new PaymentResult(true, "unused", null), "mock-success");
        OrderService service = CreateService(repository, gateway);
        OrderRequest request = CreateRequest("mock-success");

        await Should.ThrowAsync<OrderProcessingException>(() => service.SubmitOrderAsync(request));

        gateway.Verify(x => x.ProcessPaymentAsync(It.IsAny<OrderRequest>()), Times.Never);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenGatewayIsUnsupported_ThrowsWithoutAccessingRepository()
    {
        Mock<IOrderRepository> repository = new();
        OrderService service = CreateService(repository, CreateGateway(new PaymentResult(true, "unused", null)));
        OrderRequest request = new()
        {
            OrderNumber = "ORDER-1",
            UserId = "USER-1",
            Amount = 25.50m,
            PaymentGatewayId = "unknown"
        };

        await Should.ThrowAsync<UnsupportedPaymentGatewayException>(() => service.SubmitOrderAsync(request));

        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenConcurrentOrderAlreadyExists_ReturnsDuplicateWithoutChargingAgain()
    {
        Order existingOrder = CreateOrder(OrderPaymentStatus.Paid);
        Mock<IOrderRepository> repository = CreateRepositoryWithNoExistingOrder();
        repository.Setup(x => x.TryAddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderCreationResult(existingOrder, false));
        Mock<IPaymentGateway> gateway = CreateGateway(new PaymentResult(true, "unused", null));
        OrderService service = CreateService(repository, gateway);

        OrderSubmissionResult result = await service.SubmitOrderAsync(CreateRequest());

        result.IsDuplicate.ShouldBeTrue();
        result.Receipt.Confirmation.ShouldBe("CONFIRM-123");
        gateway.Verify(x => x.ProcessPaymentAsync(It.IsAny<OrderRequest>()), Times.Never);
    }

    [Fact]
    public async Task GetPaymentReceiptAsync_WhenOrderIsPaid_ReturnsReceipt()
    {
        Order order = CreateOrder(OrderPaymentStatus.Paid);
        Mock<IOrderRepository> repository = CreateRepositoryReturning(order);
        OrderService service = CreateService(repository, CreateGateway(new PaymentResult(true, "unused", null)));

        PaymentReceipt receipt = await service.GetPaymentReceiptAsync("ORDER-1");

        order.PaidAt.ShouldNotBeNull();
        receipt.OrderNumber.ShouldBe("ORDER-1");
        receipt.Amount.ShouldBe(25.50m);
        receipt.Timestamp.ShouldBe(order.PaidAt.Value);
        receipt.Confirmation.ShouldBe("CONFIRM-123");
    }

    [Fact]
    public async Task GetPaymentReceiptAsync_WhenOrderDoesNotExist_ThrowsNotFound()
    {
        Mock<IOrderRepository> repository = CreateRepositoryReturning(null);
        OrderService service = CreateService(repository, CreateGateway(new PaymentResult(true, "unused", null)));

        await Should.ThrowAsync<OrderNotFoundException>(() => service.GetPaymentReceiptAsync("MISSING"));
    }

    [Fact]
    public async Task GetPaymentReceiptAsync_WhenOrderIsNotPaid_ThrowsReceiptNotFound()
    {
        Mock<IOrderRepository> repository = CreateRepositoryReturning(CreateOrder(OrderPaymentStatus.Declined));
        OrderService service = CreateService(repository, CreateGateway(new PaymentResult(true, "unused", null)));

        await Should.ThrowAsync<ReceiptNotFoundException>(() => service.GetPaymentReceiptAsync("ORDER-1"));
    }

    private static OrderService CreateService(
        Mock<IOrderRepository> repository,
        params Mock<IPaymentGateway>[] gateways)
    {
        return new OrderService(repository.Object, gateways.Select(gateway => gateway.Object));
    }

    private static Mock<IOrderRepository> CreateRepositoryWithNoExistingOrder()
    {
        Mock<IOrderRepository> repository = new();
        repository.Setup(x => x.GetSingleAsync(
                It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        repository.Setup(x => x.TryAddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns((Order order, CancellationToken _) =>
                Task.FromResult(new OrderCreationResult(order, true)));
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        return repository;
    }

    private static Mock<IOrderRepository> CreateRepositoryReturning(Order? order)
    {
        Mock<IOrderRepository> repository = new();
        repository.Setup(x => x.GetSingleAsync(
                It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        return repository;
    }

    private static Mock<IPaymentGateway> CreateGateway(PaymentResult result, string gatewayId = GatewayId)
    {
        Mock<IPaymentGateway> gateway = new();
        gateway.SetupGet(x => x.Id).Returns(gatewayId);
        gateway.Setup(x => x.ProcessPaymentAsync(It.IsAny<OrderRequest>()))
            .ReturnsAsync(result);
        return gateway;
    }

    private static OrderRequest CreateRequest(string gatewayId = GatewayId, decimal amount = 25.50m)
    {
        return new OrderRequest
        {
            OrderNumber = "ORDER-1",
            UserId = "USER-1",
            Amount = amount,
            PaymentGatewayId = gatewayId
        };
    }

    private static Order CreateOrder(OrderPaymentStatus status)
    {
        return new Order
        {
            OrderNumber = "ORDER-1",
            UserId = "USER-1",
            Amount = 25.50m,
            PaymentGatewayId = GatewayId,
            Status = status,
            PaidAt = DateTimeOffset.UtcNow,
            Confirmation = "CONFIRM-123"
        };
    }
}
