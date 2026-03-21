namespace Wiaoj.Modulith;
/// <summary>
/// Optional interface for modules that need to execute async logic during
/// the application lifecycle.
/// <para>
/// Implement this alongside <see cref="IModule"/> when your module needs to:
/// warm up caches, establish connections, run migrations, or gracefully drain work
/// before shutdown.
/// </para>
/// <para>
/// Hooks are invoked in topological dependency order — a module's <c>OnStarting</c>
/// is always called after its dependencies' <c>OnStarting</c> completes.
/// <c>OnStopping</c> is invoked in reverse order.
/// </para>
/// </summary>
/// <example>
/// <code>
/// public sealed class OrdersModule : IModule, IModuleLifecycle {
///     public string Name => "Orders";
///     public void Register(...) { ... }
///
///     public async Task OnStarting(CancellationToken ct) {
///         await _cache.WarmUpAsync(ct);
///     }
///
///     public Task OnStarted(CancellationToken ct) {
///         _logger.LogInformation("Orders module ready.");
///         return Task.CompletedTask;
///     }
///
///     public async Task OnStopping(CancellationToken ct) {
///         await _backgroundQueue.DrainAsync(ct);
///     }
/// }
/// </code>
/// </example>
public interface IModuleLifecycle {

    /// <summary>
    /// Called before the application starts accepting requests.
    /// Awaited before proceeding to the next module.
    /// </summary>
    Task OnStarting(CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after the host has fully started and is accepting requests.
    /// Ideal for fire-and-forget warm-up tasks or diagnostic logging.
    /// </summary>
    Task OnStarted(CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the application is shutting down, in reverse boot order.
    /// Use this to drain queues, close connections, or flush buffers.
    /// </summary>
    Task OnStopping(CancellationToken cancellationToken = default);
}