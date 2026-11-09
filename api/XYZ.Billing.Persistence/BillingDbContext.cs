using Microsoft.EntityFrameworkCore;
using XYZ.Billing.Domain.Entities;
using XYZ.Billing.Persistence.EntityConfigurations;

namespace XYZ.Billing.Persistence;

public class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        new OrderConfiguration(modelBuilder.Entity<Order>());
    }
}