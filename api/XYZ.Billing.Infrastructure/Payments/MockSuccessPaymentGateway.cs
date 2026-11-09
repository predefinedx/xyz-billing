using XYZ.Billing.Application.Orders;
using XYZ.Billing.Application.Payments;

namespace XYZ.Billing.Infrastructure.Payments;

public class MockSuccessPaymentGateway : IPaymentGateway
{
    public string Id => "mock-success";
    public Task<PaymentResult> ProcessPaymentAsync(OrderRequest order)
    {
        string confirmation = $"MOCK-{Guid.NewGuid():N}".ToUpperInvariant();
        return Task.FromResult(new PaymentResult(true, confirmation, null));
    }
}