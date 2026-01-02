using System.Linq.Expressions;
using System.Reflection;
using Wiaoj.Ddd.Abstractions.ValueObjects;

namespace Wiaoj.Ddd.Abstractions;
/// <summary>
/// Marks an aggregate root as concurrency-safe using Optimistic Concurrency Control.
/// </summary>
public interface IConcurrencySafe {
    /// <summary>
    /// The version token from the database.
    /// </summary>
    RowVersion Version { get; set; }
}

/// <summary>
/// Represents a strongly-typed enum class that allows behavior/logic unlike standard enums.
/// </summary>
public abstract record Enumeration<TId> : IComparable<Enumeration<TId>> where TId : notnull {
    public TId Id { get; }
    public string Name { get; }

    protected Enumeration(TId id, string name) {
        this.Id = id;
        this.Name = name;
    }

    public override string ToString() {
        return this.Name;
    }

    public int CompareTo(Enumeration<TId>? other) {
        return other is null ? 1 : Comparer<TId>.Default.Compare(this.Id, other.Id);
    }

    public static IEnumerable<T> GetAll<T>() where T : Enumeration<TId> {
        return Cache<T>.Items;
    }

    private static class Cache<T> where T : Enumeration<TId> {
        public static readonly T[] Items;

        static Cache() {
            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            Items = [.. fields.Select(f => f.GetValue(null)).Cast<T>()];
        }
    }
    public static bool operator <(Enumeration<TId> left, Enumeration<TId> right) {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(Enumeration<TId> left, Enumeration<TId> right) {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(Enumeration<TId> left, Enumeration<TId> right) {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(Enumeration<TId> left, Enumeration<TId> right) {
        return left.CompareTo(right) >= 0;
    }
}

// Helper for integer-based enumerations (Most common)
public abstract record Enumeration : Enumeration<int> {
    protected Enumeration(int id, string name) : base(id, name) { }
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