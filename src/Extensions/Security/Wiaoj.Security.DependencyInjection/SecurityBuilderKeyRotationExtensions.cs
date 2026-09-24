using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Preconditions;

namespace Wiaoj.Security;

/// <summary>Configures <see cref="KeyRotationOptions"/> on an <see cref="ISecurityBuilder"/>.</summary>
public static class SecurityBuilderKeyRotationExtensions {
    /// <summary>Configures key size, rotation interval and the other <see cref="KeyRotationOptions"/>.</summary>
    /// <param name="builder">The security builder.</param>
    /// <param name="configure">Sets the options.</param>
    /// <returns>The builder, for chaining.</returns>
    public static ISecurityBuilder ConfigureKeyRotation(this ISecurityBuilder builder, Action<KeyRotationOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        return builder;
    }

    /// <summary>
    /// Binds <see cref="KeyRotationOptions"/> from <paramref name="configuration"/>, such as a <c>"Security"</c> section of
    /// appsettings.json or environment variables.
    /// </summary>
    /// <param name="builder">The security builder.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <returns>The builder, for chaining.</returns>
    public static ISecurityBuilder ConfigureKeyRotation(this ISecurityBuilder builder, IConfiguration configuration) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configuration);

        builder.Services.AddOptions<KeyRotationOptions>().Bind(configuration);
        return builder;
    }
}
