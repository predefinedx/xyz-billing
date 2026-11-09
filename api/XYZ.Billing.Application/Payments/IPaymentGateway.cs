using XYZ.Billing.Application.Orders;

namespace XYZ.Billing.Application.Payments;

public interface IPaymentGateway
{
    string Id { get; }

    Task<PaymentResult> ProcessPaymentAsync(OrderRequest order);
}