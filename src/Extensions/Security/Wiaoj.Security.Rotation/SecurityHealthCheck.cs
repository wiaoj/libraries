using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Wiaoj.Security.DependencyInjection;

namespace Wiaoj.Security;

/// <summary>
/// Health check for <typeparamref name="TContext"/>'s key ring.
/// Reports:
/// <list type="bullet">
///   <item><b>Unhealthy</b> — key ring not yet initialized (startup failure).</item>
///   <item><b>Degraded</b> — key ring loaded but active key is overdue for rotation.</item>
///   <item><b>Healthy</b> — key ring loaded and key is within rotation window.</item>
/// </list>
/// </summary>
/// <remarks>
/// Register via <c>AddSecurityHealthCheck&lt;TContext&gt;()</c> on <see cref="ISecurityBuilder"/>.
/// Kubernetes liveness/readiness example:
/// <code>
/// app.MapHealthChecks("/health/live",  new() { Predicate = r => r.Tags.Contains("live") });
/// app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });
/// </code>
/// </remarks>
public sealed class SecurityHealthCheck<TContext>(
    ManagedSecretProtector<TContext> protector,
    IEncryptionKeyStore store,
    IOptions<KeyRotationOptions> options,
    TimeProvider timeProvider) : IHealthCheck
    where TContext : ISecretContext {
    private readonly KeyRotationOptions _options = options.Value;
    private readonly string _ctx = typeof(TContext).Name;

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) {
        // ── 1. Is key ring initialized? ───────────────────────────────────────
        if(!protector.IsInitialized) {
            return HealthCheckResult.Unhealthy(
                $"[{this._ctx}] Key ring has not been initialized. " +
                "SecurityInitializationService may have failed at startup.");
        }

        // ── 2. Can we read the current key version? ───────────────────────────
        KeyVersion currentVersion;
        try {
            currentVersion = protector.CurrentKeyVersion;
        }
        catch(Exception ex) {
            return HealthCheckResult.Unhealthy(
                $"[{this._ctx}] Failed to read current key version.", ex);
        }

        // ── 3. Is the active key overdue for rotation? ────────────────────────
        try {
            IReadOnlyList<EncryptionKeyRecord> records =
                await store.LoadKeysAsync(this._ctx, cancellationToken);

            EncryptionKeyRecord? active = records
                .Where(r => !r.IsRetired && r.Version == currentVersion.Value)
                .FirstOrDefault();

            if(active is not null) {
                TimeSpan age = timeProvider.GetUtcNow() - active.CreatedAt;
                TimeSpan rotationInterval = this._options.RotationInterval;

                Dictionary<string, object> data = new() {
                    ["context"] = this._ctx,
                    ["current_version"] = currentVersion.Value,
                    ["key_age_days"] = (int)age.TotalDays,
                    ["rotation_interval"] = rotationInterval.ToString("d\\d"),
                };

                if(age > rotationInterval) {
                    return HealthCheckResult.Degraded(
                        $"[{this._ctx}] Key v{currentVersion} is overdue for rotation " +
                        $"(age {(int)age.TotalDays}d, limit {(int)rotationInterval.TotalDays}d). " +
                        "Check RotationBackgroundService logs.",
                        data: data);
                }

                return HealthCheckResult.Healthy(
                    $"[{this._ctx}] Key v{currentVersion} active " +
                    $"({(int)age.TotalDays}d / {(int)rotationInterval.TotalDays}d).",
                    data: data);
            }
        }
        catch(Exception ex) {
            // Store unreachable → degraded, not unhealthy (in-memory ring still works)
            return HealthCheckResult.Degraded(
                $"[{this._ctx}] Could not verify key age (store unavailable). " +
                $"Key ring is loaded at v{currentVersion}.", ex);
        }

        return HealthCheckResult.Healthy($"[{this._ctx}] Key ring loaded at v{currentVersion}.");
    }
}