namespace Wiaoj.Ddd;

public interface IUnitOfWork : IDisposable {
    Task<SaveChangesResult> SaveChangesAsync(CancellationToken cancellationToken);
}

public readonly record struct SaveChangesResult {
    public static readonly SaveChangesResult Empty = new(0);

    public int WrittenCount { get; }
    public bool HasChanges => WrittenCount > 0;
    public bool IsEmpty => WrittenCount == 0;

    private SaveChangesResult(int count) => WrittenCount = count;

    public static SaveChangesResult From(int count) => new(count);

    public static implicit operator bool(SaveChangesResult r) => r.HasChanges;
    public static implicit operator int(SaveChangesResult r) => r.WrittenCount;
}