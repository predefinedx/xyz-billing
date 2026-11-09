using System.ComponentModel.DataAnnotations.Schema;
using XYZ.Billing.Domain.Enums;

namespace XYZ.Billing.Domain.Entities;

[Table(("orders"))]
public class Order : BaseEntity
{
    [Column("order_number")]
    public required string OrderNumber { get; set; }
    
    [Column("user_id")]
    public required string UserId { get; set; }
    
    [Column("amount")]
    public decimal Amount { get; set; }
    
    [Column("payment_gateway_id")]
    public required string PaymentGatewayId { get; set; }
    
    [Column("description")]
    public string? Description { get; set; }
    
    [Column("status")]
    public OrderPaymentStatus Status { get; set; }
    
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
    
    [Column("paid_at")]
    public DateTimeOffset? PaidAt { get; set; }
    
    [Column("confirmation")]
    public string? Confirmation { get; set; }
    
    [Column("failure_reason")]
    public string? FailureReason { get; set; }
}