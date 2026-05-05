namespace Wiaoj.Security;

/// <summary>
/// An immutable, database-ready value object representing an encrypted secret.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never contains plaintext.</b>
/// </para>
/// <para>
/// Both fields are strongly typed:
/// <list type="bullet">
///   <item>
///     <see cref="Blob"/> is a <see cref="CipherBlob"/> — log-safe, Base64-validated,
///     and its raw content is inaccessible outside the security assembly.
///   </item>
///   <item>
///     <see cref="KeyVersion"/> is a <see cref="Wiaoj.Security.KeyVersion"/> — validated,
///     comparable, and impossible to accidentally supply as a raw <see langword="int"/>.
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Persistence:</b> Store <c>Blob.ToStorageString()</c> and <c>KeyVersion.Value</c>
/// in separate database columns.
/// </para>
/// </remarks>
/// <typeparam name="TContext">
/// Phantom type identifying the secret's domain.
/// Prevents secrets from different domains from being mixed up at compile time.
/// </typeparam>
public readonly record struct EncryptedSecret<TContext> where TContext : ISecretContext {

    /// <summary>The encrypted payload. Log-safe; raw bytes inaccessible from application code.</summary>
    public CipherBlob Blob { get; init; }

    /// <summary>
    /// The version of the key used to encrypt <see cref="Blob"/>.
    /// Required for rotation: used to select the correct decryption key
    /// and to detect whether re-encryption is needed.
    /// </summary>
    public KeyVersion KeyVersion { get; init; }

    private EncryptedSecret(CipherBlob blob, KeyVersion keyVersion) {
        this.Blob = blob;
        this.KeyVersion = keyVersion;
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Reconstructs an <see cref="EncryptedSecret{TContext}"/> from a persisted database row.
    /// </summary>
    /// <param name="storedBase64">The value from the ciphertext column.</param>
    /// <param name="keyVersion">The value from the key_version column.</param>
    public static EncryptedSecret<TContext> FromPersisted(string storedBase64, int keyVersion) {
        return new(CipherBlob.FromStorageString(storedBase64), KeyVersion.Of(keyVersion));
    }

    /// <summary>
    /// Reconstructs from already-validated value objects (e.g. from a rich domain model).
    /// </summary>
    public static EncryptedSecret<TContext> FromPersisted(CipherBlob blob, KeyVersion keyVersion) {
        return new(blob, keyVersion);
    }

    /// <summary>Internal factory used by <see cref="ISecretProtector{TContext}"/> after encryption.</summary>
    public static EncryptedSecret<TContext> Create(CipherBlob blob, KeyVersion keyVersion) {
        return new(blob, keyVersion);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Positional deconstruction.</summary>
    public void Deconstruct(out CipherBlob blob, out KeyVersion keyVersion) {
        blob = this.Blob;
        keyVersion = this.KeyVersion;
    }

    /// <summary>Safe for logging. Never exposes ciphertext.</summary>
    public override string ToString() {
        return $"[ENCRYPTED_SECRET<{typeof(TContext).Name}> key_{this.KeyVersion}]";
    }
}