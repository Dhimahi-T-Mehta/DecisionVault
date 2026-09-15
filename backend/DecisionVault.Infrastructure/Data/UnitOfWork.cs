using DecisionVault.Application.Interfaces;
using DecisionVault.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace DecisionVault.Infrastructure.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly DecisionVaultDbContext _context;
    private readonly Dictionary<Type, object> _repositories = [];
    private IDbContextTransaction? _transaction;

    public UnitOfWork(DecisionVaultDbContext context) => _context = context;

    public IRepository<User> Users => RepositoryFor<User>();
    public IRepository<Category> Categories => RepositoryFor<Category>();
    public IRepository<Decision> Decisions => RepositoryFor<Decision>();
    public IRepository<DecisionOption> Options => RepositoryFor<DecisionOption>();
    public IRepository<DecisionReason> Reasons => RepositoryFor<DecisionReason>();
    public IRepository<DecisionReview> Reviews => RepositoryFor<DecisionReview>();
    public IRepository<DecisionEvent> Events => RepositoryFor<DecisionEvent>();

    private IRepository<T> RepositoryFor<T>() where T : BaseEntity
    {
        if (_repositories.TryGetValue(typeof(T), out var existing))
            return (IRepository<T>)existing;

        var repo = new Repository<T>(_context);
        _repositories[typeof(T)] = repo;
        return repo;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transaction ??= await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        try
        {
            await _context.SaveChangesAsync(ct);
            await _transaction.CommitAsync(ct);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        try
        {
            await _transaction.RollbackAsync(ct);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _transaction = null;
    }
}
