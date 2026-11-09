using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage;
using XYZ.Billing.Domain.Entities;

namespace XYZ.Billing.Domain.Repositories;

public interface IBaseRepository<T>
    where T : BaseEntity
{
    IQueryable<T> GetQuery();
    
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    void DetachEntity(T entity);
    
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}