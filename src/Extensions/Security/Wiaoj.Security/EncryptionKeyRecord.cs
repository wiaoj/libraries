using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// Domain entity representing a versioned, master-key-wrapped encryption key.
/// Intentionally free of any persistence-framework attributes — EF Core
/// configuration lives in <c>Wiaoj.Security.EntityFrameworkCore</c>.
/// Never stores plaintext key material.
/// </summary>
public sealed class EncryptionKeyRecord {
    /// <summary>
    /// Gets or sets the unique identifier for this encryption key record.
    /// Defaults to a time-ordered UUIDv7 to ensure sequential database indexing.
    /// </summary>
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

    /// <summary>
    /// Gets a value indicating whether this key has been retired.
    /// Retired keys can still decrypt historical data but are never used for new encryptions.
    /// </summary>
    public bool IsRetired => this.RetiredAt.HasValue;

    /// <summary>
    /// Determines whether this key has exceeded its active lifespan based on the specified rotation interval.
    /// </summary>
    /// <param name="rotationInterval">The maximum duration a key remains active before rotation is required.</param>
    /// <param name="timeProvider">The time provider used to evaluate the current UTC time.</param>
    /// <returns><see langword="true"/> if the key is active and its age exceeds <paramref name="rotationInterval"/>; otherwise, <see langword="false"/>.</returns>
    public bool IsExpired(TimeSpan rotationInterval, TimeProvider timeProvider) {
        return !this.IsRetired && (timeProvider.GetUtcNow() - this.CreatedAt) > rotationInterval;
    }
}