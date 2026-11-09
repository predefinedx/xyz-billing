using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XYZ.Billing.Domain.Entities;

namespace XYZ.Billing.Persistence.EntityConfigurations;

internal class OrderConfiguration : BaseEntityConfiguration<Order>
{
    public OrderConfiguration(EntityTypeBuilder<Order> entity) : base(entity)
    {
        entity.Property(order => order.OrderNumber).HasMaxLength(100);
        
        entity.Property(order => order.UserId).HasMaxLength(100).IsRequired();
        
        entity.Property(order => order.PaymentGatewayId).HasMaxLength(64).IsRequired();
        
        entity.Property(order => order.Description).HasMaxLength(500);
        
        entity.Property(order => order.Status).HasConversion<string>().HasMaxLength(16);
        
        entity.Property(order => order.Confirmation).HasMaxLength(64);
        
        entity.Property(order => order.FailureReason).HasMaxLength(500);
        
        entity.HasIndex(e => e.OrderNumber)
            .IsUnique();
    }
}