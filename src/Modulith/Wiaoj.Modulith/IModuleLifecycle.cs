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
///     public async Task OnStarting(CancellationToken cancellationToken ) {
///         await _cache.WarmUpAsync(cancellationToken);
///     }
///
///     public Task OnStarted(CancellationToken cancellationToken ) {
///         _logger.LogInformation("Orders module ready.");
///         return Task.CompletedTask;
///     }
///
///     public async Task OnStopping(CancellationToken cancellationToken ) {
///         await _backgroundQueue.DrainAsync(cancellationToken);
///     }
/// }
/// </code>
/// </example>
public interface IModuleLifecycle {

    /// <summary>
    /// Called before the host starts accepting requests.
    /// Use this for database migrations, cache warm-up, or background connection warm-up.
    /// </summary>
    Task OnStarting(IServiceProvider serviceProvider, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called after the host has fully started and is ready to accept requests.
    /// Non-fatal: exceptions are logged and do not abort the host.
    /// </summary>
    Task OnStarted(IServiceProvider serviceProvider, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called during host shutdown, in reverse topological boot order.
    /// Use this to flush queues, cancel module-specific timers, or release resources.
    /// </summary>
    Task OnStopping(IServiceProvider serviceProvider, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }
}