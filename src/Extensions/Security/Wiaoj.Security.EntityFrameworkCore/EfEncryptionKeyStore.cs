using Microsoft.EntityFrameworkCore;

namespace Wiaoj.Security.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IEncryptionKeyStore"/>.
/// Registered automatically by <c>AddEntityFrameworkKeyStore&lt;TDbContext&gt;()</c>.
/// </summary>
public sealed class EfEncryptionKeyStore<TDbContext>(TDbContext dbContext, TimeProvider timeProvider) : IEncryptionKeyStore
    where TDbContext : DbContext, IEncryptionKeyDbContext {

    /// <inheritdoc />
    public async Task<IReadOnlyList<EncryptionKeyRecord>> LoadKeysAsync(string contextName, CancellationToken cancellationToken = default) {
        return await dbContext.EncryptionKeys
            .Where(k => k.ContextName == contextName)
            .OrderBy(k => k.Version)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EncryptionKeyRecord> SaveKeyAsync(EncryptionKeyRecord record, CancellationToken cancellationToken = default) {
        dbContext.EncryptionKeys.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return record;
    }

    /// <inheritdoc />
    public async Task RetireKeyAsync(string contextName, int version, CancellationToken cancellationToken = default) {
        EncryptionKeyRecord? record = await dbContext.EncryptionKeys
            .FirstOrDefaultAsync(k => k.ContextName == contextName && k.Version == version, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"No key found for context '{contextName}' version {version}.");

        if(!record.IsRetired) {
            record.RetiredAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}