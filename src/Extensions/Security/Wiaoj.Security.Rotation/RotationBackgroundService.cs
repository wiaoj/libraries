using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wiaoj.Security;

/// <summary>
/// Long-running background service that periodically checks whether
/// the encryption key for <typeparamref name="TContext"/> needs rotation
/// and delegates to <see cref="KeyRotationService{TContext}"/> if so.
/// </summary>
/// <remarks>
/// <para>
/// Runs once on startup, then repeats at <see cref="KeyRotationOptions.CheckInterval"/>
/// (default: every 6 hours).
/// </para>
/// <para>
/// Transient errors (DB unavailable, network blip) are logged and swallowed —
/// the next tick will retry. Cancellation (graceful shutdown) propagates cleanly.
/// </para>
/// </remarks>
public sealed class RotationBackgroundService<TContext>(IServiceScopeFactory scopeFactory,
                                                        IOptions<KeyRotationOptions> options,
                                                        ILogger<RotationBackgroundService<TContext>> logger)
    : BackgroundService where TContext : ISecretContext {
    private readonly KeyRotationOptions _options = options.Value;
    private readonly string _ctx = typeof(TContext).Name;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        logger.LogInformation(
            "[{Ctx}] Rotation background service started. " +
            "RotationInterval={Rotation:d\\d}, CheckInterval={Check}.",
            this._ctx, this._options.RotationInterval, this._options.CheckInterval);

        TimeSpan delayUntilNextTick = await CheckAndRotateAsync(stoppingToken);

        while(!stoppingToken.IsCancellationRequested) {
            try {
                await Task.Delay(delayUntilNextTick, stoppingToken);
                delayUntilNextTick = await CheckAndRotateAsync(stoppingToken);
            }
            catch(TaskCanceledException) {
                break;
            }
        }
    }

    private async Task<TimeSpan> CheckAndRotateAsync(CancellationToken cancellationToken) {
        try {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

            KeyRotationService<TContext> keyRotationService = scope.ServiceProvider.GetRequiredService<KeyRotationService<TContext>>();

            bool rotated = await keyRotationService.RotateIfNeededAsync(cancellationToken);

            if(rotated)
                logger.LogInformation("[{Ctx}] Scheduled rotation completed.", this._ctx);

            return this._options.CheckInterval;
        }
        catch(OperationCanceledException) {
            // Application is shutting down
            throw;
        }
        catch(Exception ex) {
            // Log and continue — next tick will retry
            logger.LogError(ex,
                "[{Ctx}] Rotation check failed. Will retry in {Interval}.",
                this._ctx, this._options.CheckInterval);
            return this._options.RetryIntervalOnError;
        }
    }
}