using Microsoft.AspNetCore.Diagnostics;
using XYZ.Billing.Application.Exceptions;

namespace XYZ.Billing.Api.ErrorHandling;

public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            UnsupportedPaymentGatewayException => (StatusCodes.Status400BadRequest, "Unsupported payment gateway", "unsupported_payment_gateway"),
            OrderNumberConflictException => (StatusCodes.Status409Conflict, "Order number conflict", "order_number_conflict"),
            PaymentDeclinedException => (StatusCodes.Status402PaymentRequired, "Payment declined", "payment_declined"),
            OrderProcessingException => (StatusCodes.Status409Conflict, "Order is being processed", "order_processing"),
            OrderNotFoundException => (StatusCodes.Status404NotFound, "Order not found", "order_not_found"),
            ReceiptNotFoundException => (StatusCodes.Status404NotFound, "Receipt not found", "receipt_not_found"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "internal_server_error")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled request failure. TraceId: {TraceId}", httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(
                exception,
                "Request rejected with {ErrorCode} ({ExceptionType}). TraceId: {TraceId}",
                code, exception.GetType().Name, httpContext.TraceIdentifier);
        }

        await Results.Problem(
            statusCode: status,
            title: title,
            detail: status == StatusCodes.Status500InternalServerError
                ? "The request could not be completed. Contact support with the trace ID."
                : exception.Message,
            instance: httpContext.Request.Path.Value,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            }).ExecuteAsync(httpContext);

        return true;
    }
}