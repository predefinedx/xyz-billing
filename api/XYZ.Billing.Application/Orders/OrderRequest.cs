using System.ComponentModel.DataAnnotations;

namespace XYZ.Billing.Application.Orders;

public class OrderRequest : IValidatableObject
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string OrderNumber { get; init; }
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string UserId { get; init; }
    
    [Range(typeof(decimal), "0.01", "999999999.99", ParseLimitsInInvariantCulture = true)]
    public required decimal Amount { get; init; }
    
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string PaymentGatewayId { get; init; }
    
    [StringLength(500)]
    public string? Description { get; init; }
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (decimal.Round(Amount, 2) != Amount)
        {
            yield return new ValidationResult(
                "Amount must have at most two decimal places.",
                [nameof(Amount)]);
        }
    }
}