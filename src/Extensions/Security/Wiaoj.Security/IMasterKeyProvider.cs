using Wiaoj.Primitives;
using Wiaoj.Security.MasterKeyProviders;

namespace Wiaoj.Security;

/// <summary>
/// Provides access to the master key used to wrap (encrypt) individual encryption
/// keys before they are persisted to the database.
/// </summary>
/// <remarks>
/// <para>
/// The master key itself never touches the database — it lives in a secure external
/// store. Implementations include:
/// <list type="bullet">
///   <item><see cref="EnvironmentMasterKeyProvider"/> — env var / appsettings (dev/staging)</item>
///   <item>Azure Key Vault — recommended for production</item>
///   <item>AWS KMS — recommended for production</item>
///   <item>HashiCorp Vault — recommended for on-prem production</item>
/// </list>
/// </para>
/// <para>
/// The caller is responsible for disposing the returned <see cref="Secret{T}"/>.
/// </para>
/// </remarks>
public interface IMasterKeyProvider {
    /// <summary>
    /// Returns the master key. The caller must dispose the returned secret after use.
    /// </summary>
    ValueTask<MasterKey> GetMasterKeyAsync(CancellationToken cancellationToken = default);
}