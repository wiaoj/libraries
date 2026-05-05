namespace Wiaoj.Security;

/// <summary>
/// Domain entity representing a versioned, master-key-wrapped encryption key.
/// Intentionally free of any persistence-framework attributes — EF Core
/// configuration lives in <c>Wiaoj.Security.EntityFrameworkCore</c>.
/// Never stores plaintext key material.
/// </summary>
public sealed class EncryptionKeyRecord {
    public Guid Id { get; set; } = Guid.CreateVersion7(); 

    /// <summary>
    /// The name of the <see cref="ISecretContext"/> type this key belongs to.
    /// E.g. "WebhookSigningContext".
    /// </summary>
    public string ContextName { get; set; } = string.Empty;

    /// <summary>Monotonically increasing version number within this context.</summary>
    public int Version { get; set; }

    /// <summary>
    /// The AES key material encrypted with the master key, stored as Base64.
    /// Format (before Base64): nonce[12] | auth_tag[16] | ciphertext[N]
    /// </summary>
    public string WrappedKeyMaterial { get; set; } = string.Empty;

    /// <summary>UTC timestamp when this key was generated.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC timestamp when this key was retired. Null = still active.</summary>
    public DateTimeOffset? RetiredAt { get; set; }

    public bool IsRetired => RetiredAt.HasValue;

    /// <summary>
    /// Returns true if this key has been active longer than <paramref name="rotationInterval"/>.
    /// </summary>
    public bool IsExpired(TimeSpan rotationInterval, TimeProvider timeProvider)
        => !IsRetired && (timeProvider.GetUtcNow() - CreatedAt) > rotationInterval;
}
