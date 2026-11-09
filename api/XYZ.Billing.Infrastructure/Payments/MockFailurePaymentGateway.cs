using XYZ.Billing.Application.Orders;
using XYZ.Billing.Application.Payments;

namespace XYZ.Billing.Infrastructure.Payments;

public class MockFailurePaymentGateway : IPaymentGateway
{
    public string Id => "mock-failure";
    
    public Task<PaymentResult> ProcessPaymentAsync(OrderRequest order)
    {
        return Task.FromResult(new PaymentResult(false, null, "Payment was declined by the mock gateway."));
    }
}