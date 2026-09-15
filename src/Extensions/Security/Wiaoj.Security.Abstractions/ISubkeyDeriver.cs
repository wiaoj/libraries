namespace Wiaoj.Security;

/// <summary>
/// Derives purpose-specific keys from the versioned keys of a secret domain, without exposing the keys themselves.
/// </summary>
/// <remarks>
/// <para>
/// For a feature that needs its own key but belongs to the domain's key lifecycle — for example encrypting identifiers,
/// where the output must be a fixed-size block rather than an AES-GCM packet. The feature derives a subkey per key
/// version and so rotates with the domain: new data uses <see cref="CurrentKeyVersion"/>, and data written under an
/// earlier version stays readable while that version remains in <see cref="KeyVersions"/>.
/// </para>
/// <para>
/// A subkey is deterministic: the same version and purpose always derive the same bytes. Different purposes derive
/// unrelated keys, so a subkey cannot be used to learn anything about the domain key or another purpose's subkey.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The secret domain.</typeparam>
public interface ISubkeyDeriver<TContext> where TContext : ISecretContext {
    /// <summary>Gets the version of the key currently used for new data.</summary>
    KeyVersion CurrentKeyVersion { get; }

    /// <summary>Gets every key version available, current and retired.</summary>
    IReadOnlyCollection<KeyVersion> KeyVersions { get; }

    /// <summary>
    /// Derives a subkey for <paramref name="purpose"/> from the key with <paramref name="version"/> into
    /// <paramref name="destination"/>.
    /// </summary>
    /// <param name="version">The key version; current or retired.</param>
    /// <param name="purpose">
    /// A fixed, non-empty label naming what the subkey is for, such as <c>"wiaoj.identifiers"</c>. Never secret or
    /// user-supplied data.
    /// </param>
    /// <param name="destination">Receives the subkey; its length is the subkey length, at most 8,160 bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="purpose"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="destination"/> is empty or longer than 8,160 bytes.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="version"/> is not available.</exception>
    void DeriveSubkey(KeyVersion version, ReadOnlySpan<byte> purpose, Span<byte> destination);
}
