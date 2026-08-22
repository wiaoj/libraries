using System.Diagnostics.CodeAnalysis;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// An immutable, type-safe value object representing an encrypted secret bound to a specific domain context (<typeparamref name="TContext"/>).
/// </summary>
/// <typeparam name="TContext">
/// Phantom type representing the secret's isolated domain (ensures compile-time domain separation).
/// </typeparam>
/// <remarks>
/// <para>
/// <b>Plaintext Safety:</b> This object never contains plaintext; it holds only the ciphertext blob and the encryption key version.
/// </para>
/// <para>
/// <b>Compact Token Format:</b> <see cref="ToCompactString"/> produces a URL-safe token in the format <c>v{version}.{base64url_blob}</c> (e.g. <c>v1.AQIDBA...</c>).
/// </para>
/// </remarks>
public readonly record struct EncryptedSecret<TContext> where TContext : ISecretContext {
    private const char Separator = '.';
    private const char VersionPrefix = 'v';

    /// <summary>
    /// Gets the Base64Url-encoded ciphertext blob.
    /// </summary>
    public CipherBlob Blob { get; init; }

    /// <summary>
    /// Gets the version of the key used to encrypt this secret.
    /// </summary>
    public KeyVersion KeyVersion { get; init; }

    private EncryptedSecret(CipherBlob blob, KeyVersion keyVersion) {
        this.Blob = blob;
        this.KeyVersion = keyVersion;
    }

    // ── Factories ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reconstructs an <see cref="EncryptedSecret{TContext}"/> from separate persistent storage values.
    /// </summary>
    /// <param name="storedBase64Url">The Base64Url ciphertext string stored in the database.</param>
    /// <param name="keyVersion">The key version integer stored in the database.</param>
    /// <returns>A reconstructed <see cref="EncryptedSecret{TContext}"/>.</returns>
    public static EncryptedSecret<TContext> FromPersisted(string storedBase64Url, int keyVersion) {
        return new(CipherBlob.FromStorageString(storedBase64Url), KeyVersion.Of(keyVersion));
    }

    /// <summary>
    /// Reconstructs an <see cref="EncryptedSecret{TContext}"/> from pre-validated value objects.
    /// </summary>
    /// <param name="blob">The validated <see cref="CipherBlob"/>.</param>
    /// <param name="keyVersion">The validated <see cref="KeyVersion"/>.</param>
    /// <returns>A reconstructed <see cref="EncryptedSecret{TContext}"/>.</returns>
    public static EncryptedSecret<TContext> FromPersisted(CipherBlob blob, KeyVersion keyVersion) {
        return new(blob, keyVersion);
    }

    /// <summary>
    /// Factory used by <see cref="ISecretProtector{TContext}"/> to wrap encryption results.
    /// </summary>
    public static EncryptedSecret<TContext> Create(CipherBlob blob, KeyVersion keyVersion) {
        return new(blob, keyVersion);
    }

    // ── Token Serialization (Format: v{N}.uExy...) ─────────────────────────────

    /// <summary>
    /// Serializes the key version and ciphertext blob into a compact, URL-safe string.
    /// Format: <c>v{version}.{base64url_blob}</c> (e.g. <c>v1.AQIDBA...</c>).
    /// </summary>
    /// <returns>A compact string representation suitable for headers, cookies, URLs, or storage.</returns>
    public string ToCompactString() {
        return $"{VersionPrefix}{this.KeyVersion.Value}{Separator}{this.Blob.ToStorageString()}";
    }

    /// <summary>
    /// Parses a compact string representation (<c>v{version}.{blob}</c>) into an <see cref="EncryptedSecret{TContext}"/>.
    /// </summary>
    /// <param name="compactString">The compact string to parse.</param>
    /// <returns>The parsed <see cref="EncryptedSecret{TContext}"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="compactString"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when the string structure or key version is invalid.</exception>
    public static EncryptedSecret<TContext> Parse(string compactString) {
        Preca.ThrowIfNullOrWhiteSpace(compactString);
        return Parse(compactString.AsSpan());
    }

    /// <summary>
    /// Parses a compact string representation (<c>v{version}.{blob}</c>) into an <see cref="EncryptedSecret{TContext}"/>.
    /// </summary>
    /// <param name="compactString">The compact string to parse.</param>
    /// <returns>The parsed <see cref="EncryptedSecret{TContext}"/> instance.</returns> 
    /// <exception cref="FormatException">Thrown when the string structure or key version is invalid.</exception>
    public static EncryptedSecret<TContext> Parse(ReadOnlySpan<char> compactString) {
        if(!TryParse(compactString, out EncryptedSecret<TContext> result)) {
            throw new FormatException(
                $"Invalid EncryptedSecret token format for context '{typeof(TContext).Name}'. Expected format: 'v{{version}}.{{base64url_blob}}'.");
        }
        return result;
    }

    /// <summary>
    /// Attempts to parse a compact string representation into an <see cref="EncryptedSecret{TContext}"/>.
    /// </summary>
    /// <param name="compactString">The compact string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed secret if successful; otherwise, default.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? compactString, out EncryptedSecret<TContext> result) {
        if(string.IsNullOrWhiteSpace(compactString)) {
            result = default;
            return false;
        }
        return TryParse(compactString.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a compact character span into an <see cref="EncryptedSecret{TContext}"/> without heap allocations.
    /// </summary>
    /// <param name="compactString">The character span containing the compact string.</param>
    /// <param name="result">When this method returns, contains the parsed secret if successful; otherwise, default.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> compactString, out EncryptedSecret<TContext> result) {
        result = default;

        int dotIndex = compactString.IndexOf(Separator);
        // Minimum valid prefix length check: 'v1.' requires at least 3 characters
        if(dotIndex < 2) return false;

        ReadOnlySpan<char> versionPart = compactString[..dotIndex];
        if(versionPart[0] != VersionPrefix) return false;

        if(!int.TryParse(versionPart[1..], out int version) || version < 0) return false;

        ReadOnlySpan<char> blobPart = compactString[(dotIndex + 1)..];
        if(blobPart.IsEmpty) return false;

        try {
            Base64UrlString base64Url = Base64UrlString.Parse(blobPart.ToString());
            CipherBlob blob = CipherBlob.From(base64Url);

            result = new EncryptedSecret<TContext>(blob, KeyVersion.Of(version));
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

    // ── Deconstruction & Display ──────────────────────────────────────────────

    /// <summary>
    /// Deconstructs the encrypted secret into its constituent components.
    /// </summary>
    /// <param name="blob">The ciphertext blob.</param>
    /// <param name="keyVersion">The key version.</param>
    public void Deconstruct(out CipherBlob blob, out KeyVersion keyVersion) {
        blob = this.Blob;
        keyVersion = this.KeyVersion;
    }

    /// <summary>
    /// Returns a log-safe representation of this secret, displaying only the context domain and key version.
    /// </summary>
    /// <returns><c>"[ENCRYPTED_SECRET&lt;ContextName&gt; vN]"</c></returns>
    public override string ToString() {
        return $"[ENCRYPTED_SECRET<{typeof(TContext).Name}> {this.KeyVersion}]";
    }
}