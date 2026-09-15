using System.Linq.Expressions;
using DecisionVault.Application.Interfaces;
using DecisionVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DecisionVault.Infrastructure.Data;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly DecisionVaultDbContext Context;
    private readonly DbSet<T> _set;

    public Repository(DecisionVaultDbContext context)
    {
        Context = context;
        _set = context.Set<T>();
    }

    public IQueryable<T> Query(bool asNoTracking = true) =>
        asNoTracking ? _set.AsNoTracking() : _set;

    public IQueryable<T> QueryWhere(Expression<Func<T, bool>> predicate, bool asNoTracking = true) =>
        Query(asNoTracking).Where(predicate);

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _set.FindAsync([id], ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await _set.AddAsync(entity, ct);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);
}
