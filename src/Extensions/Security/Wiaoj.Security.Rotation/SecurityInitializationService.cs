using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wiaoj.Security;

/// <summary>
/// Eagerly initializes <see cref="ManagedSecretProtector{TContext}"/> during application startup.
/// </summary>
/// <remarks>
/// <para>
/// Because <see cref="ManagedSecretProtector{TContext}"/> uses <c>AsyncLazy&lt;T&gt;</c>,
/// the key ring is not loaded until the first access. This service calls
/// <see cref="ManagedSecretProtector{TContext}.EnsureInitializedAsync"/> during
/// <see cref="StartAsync"/>, so the key ring is fully loaded — and any bootstrap
/// (first-time key generation) is complete — before the application starts
/// accepting requests.
/// </para>
/// <para>
/// Registered automatically by
/// <see cref="SecurityServiceExtensions.AddManagedSecretProtector{TContext,TDbContext}"/>.
/// Runs before <see cref="RotationBackgroundService{TContext}"/>.
/// </para>
/// </remarks>
internal sealed class SecurityInitializationService<TContext> : IHostedService
    where TContext : ISecretContext {

    private readonly ManagedSecretProtector<TContext> _protector;
    private readonly ILogger<SecurityInitializationService<TContext>> _logger;
    private readonly string _ctx = typeof(TContext).Name;

    public SecurityInitializationService(
        ManagedSecretProtector<TContext> protector,
        ILogger<SecurityInitializationService<TContext>> logger) {
        this._protector = protector;
        this._logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        this._logger.LogInformation("[{Ctx}] Loading key ring...", this._ctx);
        await this._protector.EnsureInitializedAsync(cancellationToken);
        this._logger.LogInformation("[{Ctx}] Key ring ready.", this._ctx);
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        return Task.CompletedTask;
    }
}