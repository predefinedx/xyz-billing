using System.Linq.Expressions;
using XYZ.Billing.Domain.Entities;

namespace XYZ.Billing.Domain.Repositories;

public interface IOrderRepository : IBaseRepository<Order>
{
    Task<Order?> GetSingleAsync(Expression<Func<Order, bool>> whereCondition, CancellationToken ct = default);
    
    Task<OrderCreationResult> TryAddAsync(Order entity, CancellationToken ct = default);

    Task<bool> TryBeginRetryAsync(Order order, string paymentGatewayId, CancellationToken ct = default);

    void Update(Order order, CancellationToken ct = default);
}

public record OrderCreationResult(Order Order, bool Created);