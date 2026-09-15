using DecisionVault.Domain.Entities;
using System.Linq.Expressions;
using DecisionVault.Domain;

using DecisionVault.Domain.Entities;
namespace DecisionVault.Application.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    IQueryable<T> Query(bool asNoTracking = true);
    IQueryable<T> QueryWhere(Expression<Func<T, bool>> predicate, bool asNoTracking = true);
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork : IDisposable
{
    IRepository<User> Users { get; }
    IRepository<Category> Categories { get; }
    IRepository<Decision> Decisions { get; }
    IRepository<DecisionOption> Options { get; }
    IRepository<DecisionReason> Reasons { get; }
    IRepository<DecisionEvent> Events { get; }
    IRepository<DecisionReview> Reviews { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
