namespace Wiaoj.Security;

/// <summary>
/// Provides access to the *previous* master key during a master-key (Type B) rotation window.
/// </summary>
/// <remarks>
/// <para>
/// Register this provider only while a master-key rewrap is in progress. The
/// <c>MasterKeyRewrapService</c> uses it to unwrap legacy DEKs that were wrapped with
/// the old master key, then re-wraps them with the current <see cref="IMasterKeyProvider"/>.
/// </para>
/// <para>
/// In steady state (no rotation pending) leave this unregistered, or have
/// <see cref="GetPreviousMasterKeyAsync"/> return <see langword="null"/>.
/// </para>
/// </remarks>
public interface IPreviousMasterKeyProvider {
    /// <summary>
    /// Returns the previous master key, or <see langword="null"/> if no rewrap is pending.
    /// The caller must dispose the returned <see cref="MasterKey"/>.
    /// </summary>
    ValueTask<MasterKey?> GetPreviousMasterKeyAsync(CancellationToken cancellationToken = default);
}
