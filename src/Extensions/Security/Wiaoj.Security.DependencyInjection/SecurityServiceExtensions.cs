using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Preconditions;
using Wiaoj.Security;
using Wiaoj.Security.DependencyInjection.Internal;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Service collection extension methods for registering Wiaoj security core services.
/// </summary>
public static class SecurityServiceExtensions {
    /// <summary>
    /// Registers the Wiaoj security core services and returns an <see cref="ISecurityBuilder"/>
    /// for further configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The security builder.</returns>
    /// <remarks>
    /// Chain the master key, the key store and the protectors on the returned builder:
    /// <code>
    /// builder.Services
    ///     .AddWiaojSecurity()
    ///     .ConfigureKeyRotation(opts => opts.KeySizeInBits = 256)
    ///     .AddEnvironmentMasterKey()
    ///     .AddEntityFrameworkKeyStore&lt;AppDbContext&gt;()  // Wiaoj.Security.EntityFrameworkCore
    ///     .AddManagedProtector&lt;WebhookContext&gt;()        // Wiaoj.Security.Rotation
    ///     .AddDataRotator&lt;WebhookContext, WebhookDataRotator&gt;(); // Wiaoj.Security.Rotation
    /// </code>
    /// <see cref="KeyRotationOptions"/> are validated when the host starts, whether or not they were configured.
    /// </remarks>
    public static ISecurityBuilder AddWiaojSecurity(this IServiceCollection services) {
        Preca.ThrowIfNull(services);

        services
            .AddOptions<KeyRotationOptions>()
            .Validate(
                opts => {
                    try { opts.Validate(); return true; }
                    catch { return false; }
                },
                "KeyRotationOptions validation failed. " +
                "Check KeySizeInBits (128/192/256), positive intervals, and positive BatchSize.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        return new SecurityBuilder(services);
    }

    /// <summary>
    /// Registers the Wiaoj security core services and configures them inside <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adds the master key, key store, protectors and key rotation settings on the builder.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddWiaojSecurity(this IServiceCollection services, Action<ISecurityBuilder> configure) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configure);

        configure(services.AddWiaojSecurity());
        return services;
    }
}
