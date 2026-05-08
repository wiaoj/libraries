using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Wiaoj.Security.DependencyInjection;

namespace Wiaoj.Security;

/// <summary>
/// Extension methods for registering <see cref="SecurityHealthCheck{TContext}"/>
/// on <see cref="ISecurityBuilder"/>.
/// </summary>
public static class SecurityBuilderHealthCheckExtensions {

    /// <summary>
    /// Registers a <see cref="SecurityHealthCheck{TContext}"/> and adds it to the
    /// .NET health check pipeline.
    /// </summary>
    /// <param name="builder">The security builder.</param>
    /// <param name="name">
    /// Health check name shown in <c>/health</c> output.
    /// Defaults to <c>"security_{ContextName}"</c>, e.g. <c>"security_WebhookSigningContext"</c>.
    /// </param>
    /// <param name="failureStatus">
    /// Status reported when the check throws. Defaults to <see cref="HealthStatus.Unhealthy"/>.
    /// </param>
    /// <param name="tags">
    /// Optional tags for filtering. E.g. <c>["ready"]</c> to include in readiness probes only.
    /// </param>
    /// <remarks>
    /// Call <b>after</b> <c>AddManagedProtector&lt;TContext&gt;()</c>.
    /// Requires <c>Microsoft.Extensions.Diagnostics.HealthChecks</c> — already present in
    /// any ASP.NET Core app that calls <c>builder.Services.AddHealthChecks()</c>.
    /// <para>
    /// Full wiring example:
    /// <code>
    /// builder.Services
    ///     .AddWiaojSecurity()
    ///     .AddEnvironmentMasterKey()
    ///     .AddEntityFrameworkKeyStore&lt;AppDbContext&gt;()
    ///     .AddManagedProtector&lt;WebhookSigningContext&gt;()
    ///     .AddSecurityHealthCheck&lt;WebhookSigningContext&gt;(tags: ["ready", "security"]);
    ///
    /// // In Program.cs:
    /// builder.Services.AddHealthChecks(); // must be called somewhere
    /// app.MapHealthChecks("/health/ready", new() {
    ///     Predicate = r => r.Tags.Contains("ready")
    /// });
    /// </code>
    /// </para>
    /// </remarks>
    public static ISecurityBuilder AddSecurityHealthCheck<TContext>(
        this ISecurityBuilder builder,
        string? name = null,
        HealthStatus failureStatus = HealthStatus.Unhealthy,
        IEnumerable<string>? tags = null)
        where TContext : ISecretContext {
        string checkName = name ?? $"security_{typeof(TContext).Name}";

        builder.Services
            .AddHealthChecks()
            .Add(new HealthCheckRegistration(
                checkName,
                sp => new SecurityHealthCheck<TContext>(
                    sp.GetRequiredService<ManagedSecretProtector<TContext>>(),
                    sp.GetRequiredService<IEncryptionKeyStore>(),
                    sp.GetRequiredService<IOptions<KeyRotationOptions>>(),
                    sp.GetRequiredService<TimeProvider>()),
                failureStatus,
                tags));

        return builder;
    }
}