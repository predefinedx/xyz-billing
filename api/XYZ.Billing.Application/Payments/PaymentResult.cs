namespace XYZ.Billing.Application.Payments;

public record PaymentResult(bool IsSuccessful, string? Confirmation, string? FailureReason);