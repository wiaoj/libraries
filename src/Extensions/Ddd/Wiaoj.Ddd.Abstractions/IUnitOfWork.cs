namespace Wiaoj.Ddd;

public interface IUnitOfWork : IDisposable {
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}