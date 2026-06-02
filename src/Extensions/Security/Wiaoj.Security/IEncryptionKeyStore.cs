namespace Wiaoj.Security;

/// <summary>
/// Persistence abstraction for versioned encryption key records.
/// Implement this interface to plug in any storage backend
/// (EF Core, Redis, Azure Table Storage, …) without touching core logic.
/// </summary>
/// <remarks>
/// Register as Scoped — implementations typically depend on a scoped DbContext or similar.
/// </remarks>
public interface IEncryptionKeyStore {
    /// <summary>Loads key records for the given context, optionally limited to the most recent ones.</summary>
    Task<IReadOnlyList<EncryptionKeyRecord>> LoadKeysAsync(string contextName, CancellationToken ct = default);

    /// <summary>Loads a specific key version for the given context.</summary>
    Task<EncryptionKeyRecord?> GetKeyAsync(string contextName, int version, CancellationToken ct = default);

    /// <summary>Persists a newly generated key record.</summary>
    Task<EncryptionKeyRecord> SaveKeyAsync(EncryptionKeyRecord record, CancellationToken ct = default);

    /// <summary>Marks a key as retired (sets RetiredAt = UtcNow).</summary>
    Task RetireKeyAsync(string contextName, int version, CancellationToken ct = default);

    /// <summary>
    /// Replaces only the <see cref="EncryptionKeyRecord.WrappedKeyMaterial"/> of an existing key,
    /// preserving version, timestamps and retirement state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <c>MasterKeyRewrapService</c> during a Type B re-key (master / KEK compromise):
    /// every wrapped DEK — active and retired — is unwrapped with the previous master key and
    /// re-wrapped with the new one. The DEK itself never changes, so application data does not
    /// need to be re-encrypted.
    /// </para>
    /// <para>
    /// The default implementation throws <see cref="NotSupportedException"/>. Stores that want
    /// to support master-key rewrap must override this method with an in-place update.
    /// </para>
    /// </remarks>
    Task UpdateWrappedKeyAsync(string contextName, int version, string newWrappedKeyMaterial, CancellationToken ct = default)
        => throw new NotSupportedException(
            $"{GetType().Name} does not support in-place wrapped-key updates. " +
            "Override IEncryptionKeyStore.UpdateWrappedKeyAsync to enable master-key (Type B) rewrap.");
}
