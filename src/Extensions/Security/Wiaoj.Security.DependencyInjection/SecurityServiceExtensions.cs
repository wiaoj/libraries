using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using Wiaoj.Security;
using Wiaoj.Security.DependencyInjection;
using Wiaoj.Security.DependencyInjection.Internal;
using Wiaoj.Security.MasterKeyProviders;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

public static class SecurityServiceExtensions {

    // ── Entry point ───────────────────────────────────────────────────────────

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
    /// </remarks>
    public static ISecurityBuilder AddWiaojSecurity(
        this IServiceCollection services,
        Action<KeyRotationOptions>? configure = null) {
        KeyRotationOptions options = new();
        configure?.Invoke(options);
        options.Validate();

        // Singleton shared by loader, rotation service, and background service.
        services.TryAddSingleton(options);

        services.TryAddSingleton(TimeProvider.System);

        return new SecurityBuilder(services);
    }

    // ── Master key providers ──────────────────────────────────────────────────

    /// <summary>
    /// Registers <see cref="EnvironmentMasterKeyProvider"/> as the master key source.
    /// Suitable for development and staging environments.
    /// For production, use a cloud KMS provider.
    /// </summary>
    /// <param name="variableName">
    /// Name of the environment variable holding the Base64-encoded master key.
    /// Default: <c>APP_MASTER_KEY</c>.
    /// </param>
    public static ISecurityBuilder AddEnvironmentMasterKey(
        this ISecurityBuilder builder,
        string variableName = "APP_MASTER_KEY") {
        builder.Services.TryAddSingleton<IMasterKeyProvider>(
            _ => new EnvironmentMasterKeyProvider(variableName));
        return builder;
    }

    /// <summary>
    /// Registers <see cref="ConfigurationMasterKeyProvider"/> as the master key source,
    /// reading from the application's <see cref="IConfiguration"/> (e.g., appsettings.json, User Secrets).
    /// </summary>
    /// <param name="builder">The security builder to configure.</param>
    /// <param name="configKey">
    /// The configuration path for the Base64-encoded master key.
    /// Default: <c>Security:MasterKey</c>.
    /// </param>
    /// <remarks>
    /// Suitable for staging environments or local development. 
    /// Ensure the configuration value is not committed to source control if it contains a real production key.
    /// </remarks>
    public static ISecurityBuilder AddConfigurationMasterKey(
        this ISecurityBuilder builder,
        string configKey = "Security:MasterKey") {
        builder.Services.TryAddSingleton<IMasterKeyProvider>(sp => {
            IConfiguration config = sp.GetRequiredService<IConfiguration>();
            return new ConfigurationMasterKeyProvider(config, configKey);
        });
        return builder;
    }

    /// <summary>
    /// Registers <see cref="FileMasterKeyProvider"/> as the master key source,
    /// reading the Base64-encoded key from the specified file path.
    /// </summary>
    /// <param name="builder">The security builder to configure.</param>
    /// <param name="filePath">
    /// The full path to the file containing the Base64-encoded master key.
    /// Supports absolute paths or paths relative to the application base directory.
    /// </param>
    /// <remarks>
    /// Useful for containerized environments (e.g., Docker Secrets, Kubernetes Secrets) 
    /// or high-security on-premise installations where environment variables are restricted.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    public static ISecurityBuilder AddFileMasterKey(
        this ISecurityBuilder builder,
        string filePath) {
        builder.Services.TryAddSingleton<IMasterKeyProvider>(_ => new FileMasterKeyProvider(filePath));
        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IMasterKeyProvider"/> (e.g. Azure Key Vault, AWS KMS).
    /// </summary>
    public static ISecurityBuilder AddMasterKeyProvider<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
        this ISecurityBuilder builder)
        where TProvider : class, IMasterKeyProvider {
        builder.Services.TryAddSingleton<IMasterKeyProvider, TProvider>();
        return builder;
    }
}
