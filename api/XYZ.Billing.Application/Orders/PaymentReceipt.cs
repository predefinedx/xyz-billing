namespace XYZ.Billing.Application.Orders;

public record PaymentReceipt(string OrderNumber, decimal Amount, DateTimeOffset Timestamp, string Confirmation);