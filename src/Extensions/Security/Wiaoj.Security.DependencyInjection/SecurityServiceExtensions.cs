using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Security;
using Wiaoj.Security.DependencyInjection;
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
    /// <remarks>
    /// Call this first, then chain the store provider and protector registrations:
    /// <code>
    /// builder.Services
    ///     .AddWiaojSecurity(opts => opts.KeySizeInBits = 256)
    ///     .AddEnvironmentMasterKey()
    ///     .AddEntityFrameworkKeyStore&lt;AppDbContext&gt;()  // Wiaoj.Security.EntityFrameworkCore
    ///     .AddManagedProtector&lt;WebhookContext&gt;()        // Wiaoj.Security.Rotation
    ///     .AddDataRotator&lt;WebhookContext, WebhookDataRotator&gt;(); // Wiaoj.Security.Rotation
    /// </code> 
    /// Or bind from appsettings.json / environment variables:
    /// <code>
    /// builder.Services
    ///     .AddWiaojSecurity(builder.Configuration.GetSection("Security"))
    ///     ...
    /// </code>
    /// </remarks>
    public static ISecurityBuilder AddWiaojSecurity(
        this IServiceCollection services,
        Action<KeyRotationOptions>? configure = null) {
        services
            .AddOptions<KeyRotationOptions>()
            .Configure(configure ?? (_ => { }))
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
    /// Overload that binds <see cref="KeyRotationOptions"/> from an
    /// <see cref="IConfiguration"/> section (e.g. <c>builder.Configuration.GetSection("Security")</c>).
    /// </summary>  
    public static ISecurityBuilder AddWiaojSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<KeyRotationOptions>? postConfigure = null) {
        services
            .AddOptions<KeyRotationOptions>()
            .Bind(configuration)
            .Configure(postConfigure ?? (_ => { }))
            .Validate(
                opts => {
                    try { opts.Validate(); return true; }
                    catch { return false; }
                },
                "KeyRotationOptions validation failed.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        return new SecurityBuilder(services);
    }
}
