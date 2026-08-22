using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using Wiaoj.Security.DependencyInjection;
using Wiaoj.Security.MasterKeyProviders;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Security;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides extension methods on <see cref="ISecurityBuilder"/> for configuring master key providers.
/// </summary>
public static class SecurityBuilderMasterKeyExtensions {

    /// <summary>
    /// Registers <see cref="EnvironmentMasterKeyProvider"/> as the master key source.
    /// Suitable for development and staging environments.
    /// For production, use a cloud KMS provider.
    /// </summary>
    /// <param name="builder">The security builder to configure.</param>
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

    /// <summary>
    /// Registers <see cref="EnvironmentPreviousMasterKeyProvider"/> so that
    /// <c>MasterKeyRewrapService</c> can unwrap legacy DEKs during a master-key rotation.
    /// Returns <see langword="null"/> at runtime if the variable is unset, which the rewrap
    /// service treats as "no rotation pending".
    /// </summary>
    /// <param name="builder">The security builder to configure.</param>
    /// <param name="variableName">
    /// Environment variable holding the Base64-encoded previous master key.
    /// Default: <c>APP_MASTER_KEY_PREVIOUS</c>.
    /// </param>
    public static ISecurityBuilder AddEnvironmentPreviousMasterKey(
        this ISecurityBuilder builder,
        string variableName = "APP_MASTER_KEY_PREVIOUS") {
        builder.Services.TryAddSingleton<IPreviousMasterKeyProvider>(
            _ => new EnvironmentPreviousMasterKeyProvider(variableName));
        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IPreviousMasterKeyProvider"/> for the Type B rewrap window
    /// (Azure Key Vault prior version, AWS KMS schedule, etc.).
    /// </summary>
    public static ISecurityBuilder AddPreviousMasterKeyProvider<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
        this ISecurityBuilder builder)
        where TProvider : class, IPreviousMasterKeyProvider {
        builder.Services.TryAddSingleton<IPreviousMasterKeyProvider, TProvider>();
        return builder;
    }
}
