using System.Linq.Expressions;
using System.Reflection;
using Wiaoj.Ddd.ValueObjects;

namespace Wiaoj.Ddd;
/// <summary>
/// Marks an aggregate root as concurrency-safe using Optimistic Concurrency Control.
/// </summary>
public interface IConcurrencySafe {
    /// <summary>
    /// The version token from the database.
    /// </summary>
    RowVersion Version { get; }
}

public interface IRepositoryMarker;

public interface IRepository<TAggregate, TId>
    : IRepositoryMarker,
      IWriteOnlyRepository<TAggregate, TId>,
      IReadOnlyRepository<TAggregate, TId>
    where TAggregate : class, IAggregate
    where TId : notnull;

public interface IWriteOnlyRepository<TAggregate, TId> : IRepositoryMarker where TAggregate : class, IAggregate where TId : notnull {
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    void Update(TAggregate aggregate);
    void Delete(TAggregate aggregate);
}

public interface IReadOnlyRepository<TAggregate, TId> : IRepositoryMarker where TAggregate : class, IAggregate where TId : notnull {
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<List<TAggregate>> ListAsync(CancellationToken cancellationToken = default);
    Task<List<TAggregate>> ListAsync(Expression<Func<TAggregate, bool>> expression, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<TAggregate, bool>> expression, CancellationToken cancellationToken = default);
    Task<long> LongCountAsync(Expression<Func<TAggregate, bool>> expression, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<TAggregate, bool>> expression, CancellationToken cancellationToken = default);
}