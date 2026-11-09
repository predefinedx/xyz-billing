using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using XYZ.Billing.Domain.Entities;
using XYZ.Billing.Domain.Repositories;

namespace XYZ.Billing.Persistence.Repositories;

public class BaseRepository<T> : IBaseRepository<T>
    where T : BaseEntity
{
    protected readonly BillingDbContext _dbContext;
    protected readonly DbSet<T> _dbSet;

    public BaseRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = _dbContext.Set<T>();
    }

    public IQueryable<T> GetQuery()
    {
        return _dbSet.AsQueryable();
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _dbContext.SaveChangesAsync(ct);
    }

    public void DetachEntity(T entity)
    {
        _dbContext.Entry(entity).State = EntityState.Detached;
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> whereCondition, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(whereCondition, ct);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        return await _dbContext.Database.BeginTransactionAsync(ct);
    }
}