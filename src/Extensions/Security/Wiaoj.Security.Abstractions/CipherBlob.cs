using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// An opaque, log-safe wrapper around a Base64-encoded AES-GCM ciphertext blob.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not <c>string</c> or <c>Base64String</c>?</b><br/>
/// A raw <c>string</c> or generic <c>Base64String</c> exposes its content via
/// <c>ToString()</c>, which means the ciphertext can silently appear in logs,
/// exception messages, or debug watches. <see cref="CipherBlob"/> overrides
/// <c>ToString()</c> to return a safe sentinel and restricts the actual
/// Base64 accessor to <c>internal</c> — only <see cref="ISecretProtector{TContext}"/>
/// can read the raw bytes.
/// </para>
/// <para>
/// <b>Validation:</b><br/>
/// Construction validates that the string is non-empty and structurally valid Base64,
/// so a <see cref="CipherBlob"/> can never wrap garbage.
/// </para>
/// <para>
/// <b>Persistence:</b><br/>
/// Serializers can access the underlying Base64 value via
/// <see cref="ToStorageString"/> which is intentionally named to communicate
/// that it should only appear in DB / transport layers, not in application logic.
/// </para>
/// </remarks>
public readonly record struct CipherBlob {
    private readonly Base64UrlString _base64;
    private CipherBlob(Base64UrlString base64) {
        this._base64 = base64;
    }

    /// <summary>
    /// Creates a <see cref="CipherBlob"/> from an already-encrypted, Base64-encoded string.
    /// Call this when loading a persisted value from the database.
    /// </summary>
    /// <param name="base64">The raw Base64Url string produced by a previous <c>Protect</c> call.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="base64"/> is null, empty, whitespace, or not valid Base64.
    /// </exception>
    public static CipherBlob FromStorageString(string base64) {
        return new(Base64UrlString.Parse(base64));
    }

    /// <summary>
    /// Internal factory: used by <see cref="ISecretProtector{TContext}"/> after completing encryption.
    /// Skips re-validation because the value was just produced by <c>Convert.ToBase64String</c>.
    /// </summary>
    public static CipherBlob From(Base64UrlString base64) {
        return new(base64);
    }

    /// <summary>
    /// Returns the raw Base64 string for persistence (database column, JSON field, etc.).
    /// </summary>
    /// <remarks>
    /// The deliberate name <c>ToStorageString</c> signals that this value belongs in a
    /// storage/transport layer only. It should never be passed into application logic
    /// that does not specifically deal with ciphertext.
    /// </remarks>
    public string ToStorageString() {
        return this._base64;
    }

    /// <summary>
    /// <b>Internal only.</b> Provides the raw Base64 string for cryptographic operations
    /// inside <see cref="ISecretProtector{TContext}"/>.
    /// Inaccessible from application code — prevents ciphertext from leaking out of the security layer.
    /// </summary>
    public string RawBase64 => this._base64.Value;

    /// <summary>
    /// Returns a safe sentinel string. The ciphertext is <b>never</b> included,
    /// making this safe for logging, exceptions, and debug watches.
    /// </summary>
    public override string ToString() {
        return "[CIPHER_BLOB]";
    }
}