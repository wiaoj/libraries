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
    /// <summary>Loads all key records for the given context (active + retired), ordered by version.</summary>
    Task<IReadOnlyList<EncryptionKeyRecord>> LoadKeysAsync(string contextName, CancellationToken ct = default);

    /// <summary>Persists a newly generated key record.</summary>
    Task<EncryptionKeyRecord> SaveKeyAsync(EncryptionKeyRecord record, CancellationToken ct = default);

    /// <summary>Marks a key as retired (sets RetiredAt = UtcNow).</summary>
    Task RetireKeyAsync(string contextName, int version, CancellationToken ct = default);
}
