using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Wiaoj.Ddd.EntityFrameworkCore;
public interface IEfcoreRepository : IRepositoryMarker;
public abstract class EfcoreRepository<TContext, TAggregate, TId>(TContext context) : IEfcoreRepository, IRepository<TAggregate, TId>
    where TContext : DbContext
    where TAggregate : class, IAggregate
    where TId : notnull {
    public DbSet<TAggregate> DbSet => context.Set<TAggregate>();
    public TContext Context => context;

    public Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default) {
        return this.DbSet.AddAsync(aggregate, cancellationToken).AsTask();
    }

    public Task<bool> AnyAsync(Expression<Func<TAggregate, bool>> expression, CancellationToken cancellationToken = default) {
        return this.DbSet.AnyAsync(expression, cancellationToken);
    }

    public Task<int> CountAsync(Expression<Func<TAggregate, bool>> expression, CancellationToken cancellationToken = default) {
        return this.DbSet.CountAsync(expression, cancellationToken);
    }

    public Task<long> LongCountAsync(Expression<Func<TAggregate, bool>> expression, CancellationToken cancellationToken = default) {
        return this.DbSet.LongCountAsync(expression, cancellationToken);
    }

    public void Delete(TAggregate aggregate) {
        this.DbSet.Remove(aggregate);
    }

    public Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default) {
        return this.DbSet.FindAsync([id], cancellationToken).AsTask();
    }

    public Task<List<TAggregate>> ListAsync(CancellationToken cancellationToken = default) {
        return this.DbSet.ToListAsync(cancellationToken);
    }

    public Task<List<TAggregate>> ListAsync(Expression<Func<TAggregate, bool>> expression, CancellationToken cancellationToken = default) {
        return this.DbSet.Where(expression).ToListAsync(cancellationToken);
    }

    public void Update(TAggregate aggregate) {
        this.DbSet.Update(aggregate);
    }
}