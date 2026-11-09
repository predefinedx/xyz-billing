using System.Linq.Expressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using XYZ.Billing.Domain.Entities;
using XYZ.Billing.Domain.Enums;
using XYZ.Billing.Domain.Repositories;

namespace XYZ.Billing.Persistence.Repositories;

public class OrderRepository : BaseRepository<Order>, IOrderRepository
{
    private const int SqliteUniqueConstraintExtendedErrorCode = 2067;
    
    public OrderRepository(BillingDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Order?> GetSingleAsync(Expression<Func<Order, bool>> whereCondition, CancellationToken ct = default)
    {
        return await GetQuery()
            .Where(whereCondition)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<OrderCreationResult> TryAddAsync(Order entity, CancellationToken ct = default)
    {
        await AddAsync(entity, ct);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteUniqueConstraintExtendedErrorCode })
        {
            // A rejected insert stays Added in EF; detach it before loading the winning order.
            DetachEntity(entity);
            
            Order? concurrentExistingOrder = await GetSingleAsync(o => o.OrderNumber == entity.OrderNumber, ct);
            if (concurrentExistingOrder == null)
            {
                throw;
            }
            
            return new OrderCreationResult(concurrentExistingOrder, false);
        }
        
        return new OrderCreationResult(entity, true);
    }

    public async Task<bool> TryBeginRetryAsync(Order order, string paymentGatewayId, CancellationToken ct = default)
    {
        int updatedRows = await _dbContext.Orders
            .Where(current =>
                current.Id == order.Id &&
                current.OrderNumber == order.OrderNumber &&
                current.UserId == order.UserId &&
                current.Amount == order.Amount &&
                current.Description == order.Description &&
                current.PaymentGatewayId == order.PaymentGatewayId &&
                current.Status == OrderPaymentStatus.Declined)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(current => current.Status, OrderPaymentStatus.Processing)
                .SetProperty(current => current.PaymentGatewayId, paymentGatewayId)
                .SetProperty(current => current.FailureReason, (string?)null)
                .SetProperty(current => current.Confirmation, (string?)null)
                .SetProperty(current => current.PaidAt, (DateTimeOffset?)null), ct);

        return updatedRows == 1;
    }

    public async Task AddAsync(Order entity, CancellationToken ct = default)
    {
        await _dbContext.Orders.AddAsync(entity, ct);
    }

    public void Update(Order order, CancellationToken ct = default)
    {
        _dbContext.Orders.Update(order);
    }
}