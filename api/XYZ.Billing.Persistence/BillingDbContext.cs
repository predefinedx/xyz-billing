using Microsoft.EntityFrameworkCore;

namespace XYZ.Billing.Persistence;

public class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    
}