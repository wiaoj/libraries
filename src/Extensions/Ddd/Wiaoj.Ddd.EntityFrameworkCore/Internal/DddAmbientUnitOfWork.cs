namespace Wiaoj.Ddd.EntityFrameworkCore.Internal;
/// <summary>
/// Scoped holder that surfaces the live <see cref="IUnitOfWork"/> (the DbContext currently being saved)
/// to pre-commit domain event handlers. The dispatcher interceptor opens a dedicated scope and seeds this
/// holder so that handlers join the same transaction regardless of how the DbContext was registered
/// (scoped, pooled, or via <c>IDbContextFactory</c>).
/// </summary>
internal sealed class DddAmbientUnitOfWork {
    public IUnitOfWork? Current { get; set; }
}
