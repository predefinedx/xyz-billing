namespace XYZ.Billing.Application.Exceptions;

public class UnsupportedPaymentGatewayException(string paymentGatewayId) : Exception($"Payment gateway '{paymentGatewayId}' is not supported.");

public class OrderNumberConflictException() : Exception("This order number has already been used with different order details.");

public class PaymentDeclinedException(string reason) : Exception(reason);

public class OrderProcessingException() : Exception("This order is already being processed. Retry later to retrieve its result.");

public class OrderNotFoundException() : Exception("No order was found with the provided order number.");

public class ReceiptNotFoundException() : Exception("No paid receipt was found for this order.");