using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wiaoj.Modulith.Internal; 
/// <summary>
/// <see cref="IHostedService"/> that drives the <see cref="IModuleLifecycle"/> hooks
/// for all active modules in topological order.
/// <para>
/// Registered automatically by <c>AddModulith()</c>. You do not need to register it manually.
/// </para>
/// <list type="bullet">
///   <item><description><c>StartAsync</c> → calls <see cref="IModuleLifecycle.OnStarting"/>, then <see cref="IModuleLifecycle.OnStarted"/> in boot order.</description></item>
///   <item><description><c>StopAsync</c>  → calls <see cref="IModuleLifecycle.OnStopping"/> in reverse boot order.</description></item>
/// </list>
/// </summary>
internal sealed class ModulithHostedService(
    ModuleRegistry registry,
    IOptions<ModulithOptions> options,
    ILogger<ModulithHostedService> logger) : IHostedService {

    private readonly ModulithOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken) {
        foreach(IModuleLifecycle module in registry.LifecycleModules) {
            string name = ((IModule)module).Name;
            logger.LogInformation("[Modulith] {Module} — OnStarting", name);

            await RunWithTimeoutAsync(
                ct => module.OnStarting(ct),
                _options.StartupHookTimeout,
                name, "OnStarting",
                cancellationToken,
                throwOnFailure: true);
        }

        foreach(IModuleLifecycle module in registry.LifecycleModules) {
            string name = ((IModule)module).Name;

            await RunWithTimeoutAsync(
                ct => module.OnStarted(ct),
                _options.StartupHookTimeout,
                name, "OnStarted",
                cancellationToken,
                throwOnFailure: false);   // non-fatal after host is up
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        foreach(IModuleLifecycle module in registry.LifecycleModules.Reverse()) {
            string name = ((IModule)module).Name;
            logger.LogInformation("[Modulith] {Module} — OnStopping", name);

            await RunWithTimeoutAsync(
                ct => module.OnStopping(ct),
                _options.ShutdownHookTimeout,
                name, "OnStopping",
                cancellationToken,
                throwOnFailure: false);  // always let all modules stop
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RunWithTimeoutAsync(
        Func<CancellationToken, Task> work,
        TimeSpan timeout,
        string moduleName,
        string hookName,
        CancellationToken ct,
        bool throwOnFailure) {

        using CancellationTokenSource cts = timeout == Timeout.InfiniteTimeSpan
            ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : CancellationTokenSource.CreateLinkedTokenSource(ct);

        if(timeout != Timeout.InfiniteTimeSpan)
            cts.CancelAfter(timeout);

        try {
            await work(cts.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested) {
            string msg = $"[Modulith] {moduleName} — {hookName} timed out after {timeout.TotalSeconds}s.";
            if(throwOnFailure) throw new TimeoutException(msg);
            logger.LogWarning(msg);
        }
        catch(Exception ex) {
            string msg = $"[Modulith] {moduleName} — {hookName} failed: {ex.Message}";
            if(throwOnFailure) {
                logger.LogError(ex, msg);
                throw;
            }
            logger.LogWarning(ex, msg);
        }
    }
}