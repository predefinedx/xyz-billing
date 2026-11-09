namespace XYZ.Billing.Application.Orders;

public record OrderSubmissionResult(PaymentReceipt Receipt, bool IsDuplicate = false);