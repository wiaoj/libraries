using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Wiaoj.Security;

// ────────────────────────────────────────────────────────────────────────────
// KeyRotationService<TContext>
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Orchestrates key rotation: checks expiry, generates new keys,
/// retires old ones, reloads the protector, and triggers data rotation.
/// </summary>
/// <remarks>
/// Register as Scoped. Called by <see cref="RotationBackgroundService{TContext}"/>
/// and optionally by a manual endpoint.
/// </remarks>
public sealed class KeyRotationService<TContext>
    where TContext : ISecretContext {
    private readonly IEncryptionKeyStore _store;
    private readonly IMasterKeyProvider _masterKeyProvider;
    private readonly ManagedSecretProtector<TContext> _protector;
    private readonly IDataRotator<TContext>? _dataRotator;
    private readonly KeyRotationOptions _options;
    private readonly ILogger<KeyRotationService<TContext>> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly string _ctx = typeof(TContext).Name;

    public KeyRotationService(
        IEncryptionKeyStore store,
        IMasterKeyProvider masterKeyProvider,
        ManagedSecretProtector<TContext> protector,
        KeyRotationOptions options,
        ILogger<KeyRotationService<TContext>> logger, 
        TimeProvider timeProvider,
        IDataRotator<TContext>? dataRotator = null) {
        this._store = store;
        this._masterKeyProvider = masterKeyProvider;
        this._protector = protector;
        this._options = options;
        this._logger = logger;
        this._timeProvider = timeProvider;
        this._dataRotator = dataRotator;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks whether the current key needs rotation and performs it if so.
    /// </summary>
    /// <returns><see langword="true"/> if a rotation was performed.</returns>
    public async Task<bool> RotateIfNeededAsync(CancellationToken ct = default) {
        IReadOnlyList<EncryptionKeyRecord> keys = await this._store.LoadKeysAsync(this._ctx, ct);

        EncryptionKeyRecord? current = keys
            .Where(k => !k.IsRetired)
            .MaxBy(k => k.Version);

        if(current is null) {
            this._logger.LogWarning("[{Ctx}] No active key found. Triggering bootstrap reload.", this._ctx);
            await this._protector.ReloadAsync(ct);
            return false;
        }

        TimeSpan age = DateTime.UtcNow - current.CreatedAt;

        if(!current.IsExpired(this._options.RotationInterval, _timeProvider)) {
            this._logger.LogDebug(
                "[{Ctx}] Key v{V} is valid (age {Age:d\\d\\ hh\\:mm}, limit {Limit:d\\d}). No rotation.",
                this._ctx, current.Version, age, this._options.RotationInterval);
            return false;
        }

        this._logger.LogInformation(
            "[{Ctx}] Key v{V} expired (age {Age:d\\d\\ hh\\:mm}). Rotating...",
            this._ctx, current.Version, age);

        await ExecuteRotationAsync(current, ct);
        return true;
    }

    /// <summary>
    /// Forces an immediate key rotation regardless of key age.
    /// Safe to call from a management endpoint (e.g. POST /admin/keys/rotate).
    /// </summary>
    public async Task ForceRotateAsync(CancellationToken ct = default) {
        IReadOnlyList<EncryptionKeyRecord> keys = await this._store.LoadKeysAsync(this._ctx, ct);
        EncryptionKeyRecord? current = keys.Where(k => !k.IsRetired).MaxBy(k => k.Version);

        if(current is null) {
            this._logger.LogWarning("[{Ctx}] No active key to rotate from. Bootstrapping.", this._ctx);
            await this._protector.ReloadAsync(ct);
            return;
        }

        this._logger.LogInformation("[{Ctx}] Forced rotation of key v{V}.", this._ctx, current.Version);
        await ExecuteRotationAsync(current, ct);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task ExecuteRotationAsync(EncryptionKeyRecord current, CancellationToken ct) {
        int newVersion = current.Version + 1;
        this._logger.LogInformation("[{Ctx}] Generating key v{New}...", this._ctx, newVersion);

        // 1. Generate & wrap new key
        byte[] keyMaterial = new byte[this._options.KeySizeInBytes];
        try {
            RandomNumberGenerator.Fill(keyMaterial);
            
            using MasterKey masterKey = await this._masterKeyProvider.GetMasterKeyAsync(ct);
             
            string wrapped = masterKey.Wrap(keyMaterial);

            EncryptionKeyRecord newRecord = new() {
                Id = Guid.CreateVersion7(),
                ContextName = this._ctx,
                Version = newVersion,
                WrappedKeyMaterial = wrapped,
                CreatedAt = _timeProvider.GetUtcNow(),
            };

            await this._store.SaveKeyAsync(newRecord, ct);
            this._logger.LogInformation("[{Ctx}] Key v{New} persisted.", this._ctx, newVersion);
        }
        finally {
            CryptographicOperations.ZeroMemory(keyMaterial);
        }

        // 2. Retire old key
        await this._store.RetireKeyAsync(this._ctx, current.Version, ct);
        this._logger.LogInformation("[{Ctx}] Key v{Old} retired.", this._ctx, current.Version);

        // 3. Hot-reload the protector (atomic swap)
        await this._protector.ReloadAsync(ct);
        this._logger.LogInformation("[{Ctx}] Protector reloaded → key v{New} is now active.", this._ctx, newVersion);

        // 4. Optionally re-encrypt application data
        if(this._options.AutoRotateData && this._dataRotator is not null)
            await RotateDataAsync(ct);
    }
     
    private async Task RotateDataAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("[{Ctx}] Starting data rotation...", _ctx);
        int total = 0;

        while(!await _dataRotator!.IsCompleteAsync(cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            int rotated = await _dataRotator.RotateBatchAsync(_options.DataRotationBatchSize, cancellationToken);
            total += rotated;
            _logger.LogDebug("[{Ctx}] Batch rotated {N} records.", _ctx, rotated);
             
            if(_options.DataRotationBatchDelay > TimeSpan.Zero) {
                await Task.Delay(_options.DataRotationBatchDelay, cancellationToken);
            }
        }

        _logger.LogInformation("[{Ctx}] Data rotation complete. Total: {Total} records.", _ctx, total);
    }
}