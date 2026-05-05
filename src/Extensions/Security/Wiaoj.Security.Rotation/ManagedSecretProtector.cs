using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Concurrency;
using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// A thread-safe, hot-reloadable singleton wrapper around <see cref="SecretProtector{TContext}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lazy initialization:</b> The inner <see cref="SecretProtector{TContext}"/> is created
/// on demand via <see cref="AsyncLazy{T}"/>. The DI factory no longer blocks the thread pool
/// with <c>.GetAwaiter().GetResult()</c>. Instead, <see cref="SecurityInitializationService{TContext}"/>
/// pre-warms the lazy during <c>IHostedService.StartAsync</c>, so by the time the application
/// starts accepting requests the key ring is already loaded.
/// </para>
/// <para>
/// <b>Hot reload:</b> <see cref="ReloadAsync"/> atomically replaces the inner lazy with a
/// newly loaded one, disposes the old lazy (which disposes the old protector and its key ring),
/// and leaves concurrent readers unaffected — they either see the old or the new protector,
/// both of which are valid.
/// </para>
/// <para>
/// <b>Thread safety:</b> The lazy reference is <see langword="volatile"/>. Concurrent reloads
/// are serialised by a <see cref="SemaphoreSlim"/>.
/// </para>
/// <para>
/// <b>Sync interface methods:</b> After the first successful initialization,
/// <see cref="AsyncLazy{T}.IsValueCreated"/> is <see langword="true"/> and
/// <c>GetValueAsync().GetAwaiter().GetResult()</c> returns the cached value without
/// entering the thread pool — it is effectively free.
/// </para>
/// </remarks>
public sealed class ManagedSecretProtector<TContext> : ISecretProtector<TContext>, IAsyncDisposable
    where TContext : ISecretContext {

    // volatile: readers always see the latest reference after a reload swap.
    private volatile AsyncLazy<SecretProtector<TContext>> _lazy;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private bool _disposed;

    public ManagedSecretProtector(
        AsyncLazy<SecretProtector<TContext>> lazy,
        IServiceScopeFactory scopeFactory) {
        this._lazy = lazy;
        this._scopeFactory = scopeFactory;
    }
     
    /// <summary>
    /// <see langword="true"/> when the key ring has been successfully loaded at least once.
    /// Used by <see cref="SecurityHealthCheck{TContext}"/>.
    /// </summary>
    public bool IsInitialized => _lazy.IsValueCreated;

    /// <summary>
    /// Ensures the inner <see cref="SecretProtector{TContext}"/> has been created.
    /// Called by <see cref="SecurityInitializationService{TContext}"/> at startup.
    /// Safe to call multiple times — subsequent calls are free.
    /// </summary>
    public async ValueTask EnsureInitializedAsync(CancellationToken ct = default) {
        await this._lazy.GetValueAsync(ct);
    }
     
    public KeyVersion CurrentKeyVersion => this.Inner.CurrentKeyVersion;

    public EncryptedSecret<TContext> Protect(ReadOnlySpan<byte> plainSecret) {
        return this.Inner.Protect(plainSecret);
    }

    public EncryptedSecret<TContext> Protect(string plainText) {
        return this.Inner.Protect(plainText);
    }

    public Secret<byte> Unprotect(in EncryptedSecret<TContext> encrypted) {
        return this.Inner.Unprotect(encrypted);
    }

    public bool NeedsRotation(in EncryptedSecret<TContext> encrypted) {
        return this.Inner.NeedsRotation(encrypted);
    }

    public EncryptedSecret<TContext> Rotate(in EncryptedSecret<TContext> encrypted) {
        return this.Inner.Rotate(encrypted);
    } 

    /// <summary>
    /// Reloads the <see cref="KeyRing{TContext}"/> from the database and atomically
    /// replaces the inner <see cref="SecretProtector{TContext}"/>.
    /// </summary>
    public async Task ReloadAsync(CancellationToken ct = default) {
        await this._reloadLock.WaitAsync(ct);
        try {
            IServiceScopeFactory scopeFactory = this._scopeFactory; // avoid closure over `this`

            AsyncLazy<SecretProtector<TContext>> newLazy = new(async innerCt => {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                KeyRingLoader<TContext> loader =
                    scope.ServiceProvider.GetRequiredService<KeyRingLoader<TContext>>();
                KeyRing<TContext> ring = await loader.LoadAsync(innerCt);
                return new SecretProtector<TContext>(ring);
            });

            // Pre-warm before the atomic swap: if this throws, the old lazy is untouched.
            await newLazy.GetValueAsync(ct);

            // Atomic reference swap — volatile write, so readers see the new lazy immediately.
            AsyncLazy<SecretProtector<TContext>> old = this._lazy;
            this._lazy = newLazy;

            // Dispose the old lazy: disposes the old SecretProtector → disposes its KeyRing
            // → zeroes all key material. Readers that captured `old` before the swap finish
            // safely; disposal only affects future accesses through the old reference.
            await old.DisposeAsync();
        }
        finally {
            this._reloadLock.Release();
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync() {
        if(!this._disposed) {
            this._disposed = true;
            await this._lazy.DisposeAsync();
            this._reloadLock.Dispose();
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the inner protector synchronously.
    /// After <see cref="EnsureInitializedAsync"/> has completed, <see cref="AsyncLazy{T}.IsValueCreated"/>
    /// is <see langword="true"/> and this is equivalent to a field read — no thread-pool involvement.
    /// If called before initialization (i.e., outside the normal startup flow), this blocks until the
    /// lazy has completed, which is acceptable as a last-resort guard.
    /// </summary>
    private SecretProtector<TContext> Inner {
        get {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            return this._lazy.GetValueAsync().GetAwaiter().GetResult();
        }
    }
}