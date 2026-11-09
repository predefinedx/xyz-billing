using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;
using XYZ.Billing.Domain.Entities;

namespace XYZ.Billing.Domain.Repositories;

public interface IBaseRepository<T>
    where T : BaseEntity
{
    IQueryable<T> GetQuery();
    
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    
    Task<bool> ExistsAsync(Expression<Func<T, bool>> whereCondition, CancellationToken ct = default);
    
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}