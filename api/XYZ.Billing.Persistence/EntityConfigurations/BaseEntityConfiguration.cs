using Microsoft.EntityFrameworkCore.Metadata.Builders;
using XYZ.Billing.Domain.Entities;

namespace XYZ.Billing.Persistence.EntityConfigurations;

internal class BaseEntityConfiguration<T> where T : BaseEntity
{
    public BaseEntityConfiguration(EntityTypeBuilder<T> entity)
    {
        entity.HasKey(e => e.Id);
        
        entity.Property(e => e.Id)
            .ValueGeneratedOnAdd();
    }
}