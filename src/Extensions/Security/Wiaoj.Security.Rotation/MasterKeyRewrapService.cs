using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// Re-wraps every <see cref="EncryptionKeyRecord"/> for <typeparamref name="TContext"/> with a
/// new master key. Used when the master / KEK has been compromised (Type B re-key).
/// </summary>
/// <remarks>
/// <para>
/// Workflow:
/// <list type="number">
///   <item>Load every key record (active + retired) for the context.</item>
///   <item>For each record, try to unwrap with the *current* master key — if that succeeds,
///         the record is already on the new master and is skipped.</item>
///   <item>Otherwise, unwrap with the <see cref="IPreviousMasterKeyProvider"/>'s key, then
///         re-wrap with the current master and persist via
///         <see cref="IEncryptionKeyStore.UpdateWrappedKeyAsync"/>.</item>
///   <item>When all records have been processed, reload the protector so any new <c>Protect</c> calls
///         pick up the freshly wrapped key ring.</item>
/// </list>
/// </para>
/// <para>
/// The DEK material itself does not change, so application data does not need to be re-encrypted.
/// Pair this service with <c>IDataRotator</c> only when the threat model also requires DEK rotation.
/// </para>
/// <para>
/// Register as Scoped via <c>AddManagedProtector&lt;TContext&gt;()</c>. The operation is idempotent:
/// running it twice is safe — the second run will find every record already current.
/// </para>
/// </remarks>
public sealed class MasterKeyRewrapService<TContext>
    where TContext : ISecretContext {

    private readonly IEncryptionKeyStore _store;
    private readonly IMasterKeyProvider _currentMaster;
    private readonly IPreviousMasterKeyProvider? _previousMaster;
    private readonly ManagedSecretProtector<TContext> _protector;
    private readonly ILogger<MasterKeyRewrapService<TContext>> _logger;
    private readonly string _ctx = typeof(TContext).Name;

    private static readonly KeyValuePair<string, object?> _ctxTag = SecurityMeter.ContextTag<TContext>();

    public MasterKeyRewrapService(
        IEncryptionKeyStore store,
        IMasterKeyProvider currentMaster,
        ManagedSecretProtector<TContext> protector,
        ILogger<MasterKeyRewrapService<TContext>> logger,
        IPreviousMasterKeyProvider? previousMaster = null) {
        this._store = store;
        this._currentMaster = currentMaster;
        this._previousMaster = previousMaster;
        this._protector = protector;
        this._logger = logger;
    }

    /// <summary>
    /// Re-wraps every key for <typeparamref name="TContext"/> with the current master key.
    /// Records that already unwrap cleanly with the current master are left untouched.
    /// </summary>
    public async Task<MasterKeyRewrapResult> RewrapAllAsync(CancellationToken cancellationToken = default) {
        long start = Stopwatch.GetTimestamp();

        if(this._previousMaster is null)
            throw new InvalidOperationException(
                "Master-key rewrap requires an IPreviousMasterKeyProvider to be registered. " +
                "Register it via .AddEnvironmentPreviousMasterKey() (or a custom provider) " +
                "before invoking RewrapAllAsync.");

        IReadOnlyList<EncryptionKeyRecord> records = await this._store.LoadKeysAsync(this._ctx, cancellationToken);
        if(records.Count == 0) {
            this._logger.LogInformation("[{Ctx}] No keys to rewrap.", this._ctx);
            return new MasterKeyRewrapResult(0, 0, 0, 0, Stopwatch.GetElapsedTime(start));
        }

        using MasterKey current = await this._currentMaster.GetMasterKeyAsync(cancellationToken);
        using MasterKey? previous = await this._previousMaster.GetPreviousMasterKeyAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "IPreviousMasterKeyProvider returned null — no previous master key configured. " +
                "Configure it (e.g. set APP_MASTER_KEY_PREVIOUS) before invoking the rewrap.");

        int rewrapped = 0;
        int alreadyCurrent = 0;
        int failed = 0;

        try {
            foreach(EncryptionKeyRecord record in records) {
                cancellationToken.ThrowIfCancellationRequested();

                if(TryUnwrap(current, record.WrappedKeyMaterial, out Secret<byte>? probe)) {
                    probe!.Value.Dispose();
                    alreadyCurrent++;
                    this._logger.LogDebug(
                        "[{Ctx}] v{V} already wrapped with current master — skipping.", this._ctx, record.Version);
                    continue;
                }

                if(!TryUnwrap(previous!.Value, record.WrappedKeyMaterial, out Secret<byte>? unwrapped)) {
                    failed++;
                    this._logger.LogError(
                        "[{Ctx}] v{V} could not be unwrapped with either current or previous master key — manual recovery required.",
                        this._ctx, record.Version);
                    continue;
                }

                string newWrapped;
                using(Secret<byte> dek = unwrapped!.Value) {
                    newWrapped = dek.Expose(current, static (master, span) => master.Wrap(span));
                }

                await this._store.UpdateWrappedKeyAsync(this._ctx, record.Version, newWrapped, cancellationToken);
                rewrapped++;
                SecurityMeter.RewrapKeyCount.Add(1, _ctxTag);
                this._logger.LogInformation("[{Ctx}] v{V} re-wrapped with new master.", this._ctx, record.Version);
            }

            // Force the in-memory ring to be re-read from store + re-unwrapped with the current master.
            await this._protector.ReloadAsync(cancellationToken);

            TimeSpan duration = Stopwatch.GetElapsedTime(start);
            SecurityMeter.RewrapCount.Add(1, _ctxTag);
            SecurityMeter.RewrapDuration.Record(duration.TotalMilliseconds, _ctxTag);

            this._logger.LogInformation(
                "[{Ctx}] Master rewrap complete — total {Total}, rewrapped {Rewrapped}, already current {Current}, failed {Failed}, duration {DurMs}ms.",
                this._ctx, records.Count, rewrapped, alreadyCurrent, failed, duration.TotalMilliseconds);

            return new MasterKeyRewrapResult(records.Count, rewrapped, alreadyCurrent, failed, duration);
        }
        catch(Exception ex) when(ex is not OperationCanceledException) {
            SecurityMeter.RewrapErrorCount.Add(1, _ctxTag);
            this._logger.LogError(ex, "[{Ctx}] Master rewrap failed after {Rewrapped} of {Total} records.",
                this._ctx, rewrapped, records.Count);
            throw;
        }
    }

    private static bool TryUnwrap(MasterKey master, string wrapped, out Secret<byte>? result) {
        try {
            result = master.Unwrap(wrapped);
            return true;
        }
        catch(CryptographicException) {
            result = null;
            return false;
        }
    }
}
