namespace Wiaoj.Ddd.Abstractions;
public interface IUnitOfWork : IDisposable {
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}