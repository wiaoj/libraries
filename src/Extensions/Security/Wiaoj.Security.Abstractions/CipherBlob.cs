using Wiaoj.Preconditions;
using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// An opaque, log-safe wrapper around a Base64Url-encoded AES-GCM ciphertext payload (Nonce + Tag + Ciphertext).
/// </summary>
/// <remarks>
/// <para>
/// <b>Log Safety:</b> <see cref="ToString"/> returns a safe sentinel string <c>[CIPHER_BLOB]</c>. 
/// Raw ciphertext will never inadvertently leak into logs, debug consoles, or exception messages.
/// </para>
/// <para>
/// <b>Encapsulation:</b> Direct access to the raw Base64Url string is restricted to <see langword="internal"/>; 
/// only the cryptography layer (<see cref="ISecretProtector{TContext}"/>) can read it for decryption.
/// </para>
/// </remarks>
public readonly record struct CipherBlob {
    /// <summary>
    /// Minimum AES-GCM payload size: 12-byte Nonce + 16-byte Auth Tag = 28 bytes.
    /// 28 bytes in Base64Url encoding corresponds to 38 characters.
    /// </summary>
    private const int MinBase64UrlLength = 38;

    private readonly Base64UrlString _base64Url;

    private CipherBlob(Base64UrlString base64Url) {
        this._base64Url = base64Url;
    }

    /// <summary>
    /// Creates a <see cref="CipherBlob"/> from an already-validated <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="base64Url">The validated Base64Url string representing an encrypted payload.</param>
    /// <returns>A valid <see cref="CipherBlob"/> instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="base64Url"/> is empty or shorter than the minimum AES-GCM payload structure (Nonce + Tag).
    /// </exception>
    public static CipherBlob From(Base64UrlString base64Url) {
        Preca.ThrowIfLessThan(
            base64Url.Value.Length,
            MinBase64UrlLength, 
            static (string name) => new ArgumentException(
                $"CipherBlob cannot be empty or shorter than the minimum valid AES-GCM packet ({MinBase64UrlLength} characters)."),
            nameof(base64Url));

        return new(base64Url);
    }

    /// <summary>
    /// Creates a <see cref="CipherBlob"/> from an already-validated <see cref="Base64UrlString"/>.
    /// </summary>
    /// <param name="base64Url">The validated Base64Url string representing the ciphertext packet.</param>
    /// <returns>A valid, log-safe <see cref="CipherBlob"/> instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="base64Url"/> is empty or shorter than the minimum AES-GCM packet size (38 characters).
    /// </exception>
    public static CipherBlob FromBase64Url(Base64UrlString base64Url) => From(base64Url);

    /// <summary>
    /// Parses a stored Base64Url string from persistence into a <see cref="CipherBlob"/>.
    /// </summary>
    /// <param name="base64Url">The raw Base64Url string read from database or external storage.</param>
    /// <returns>A valid <see cref="CipherBlob"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="base64Url"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="base64Url"/> contains invalid Base64Url characters.</exception>
    public static CipherBlob FromStorageString(string base64Url) {
        Preca.ThrowIfNullOrWhiteSpace(base64Url);
        return From(Base64UrlString.Parse(base64Url));
    }

    /// <summary>
    /// Returns the raw Base64Url string intended for storage (e.g. database columns, serialized records).
    /// </summary>
    /// <returns>The raw Base64Url string representation.</returns>
    public string ToStorageString() {
        return this._base64Url.Value;
    }

    /// <summary>
    /// Internal only: Provides the raw Base64Url string for decryption within <see cref="ISecretProtector{TContext}"/>.
    /// </summary>
    public string RawBase64Url => this._base64Url.Value;

    /// <summary>
    /// Returns a log-safe sentinel string. The underlying ciphertext is never exposed.
    /// </summary>
    /// <returns><c>"[CIPHER_BLOB]"</c></returns>
    public override string ToString() {
        return "[CIPHER_BLOB]";
    }
}