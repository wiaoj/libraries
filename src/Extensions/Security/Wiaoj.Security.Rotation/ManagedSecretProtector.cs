using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Concurrency;
using Wiaoj.Primitives;

namespace Wiaoj.Security;

/// <summary>
/// A thread-safe, hot-reloadable singleton wrapper around <see cref="SecretProtector{TContext}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lazy Initialization:</b> The inner <see cref="SecretProtector{TContext}"/> is created on demand via <see cref="AsyncLazy{T}"/>.
/// <see cref="SecurityInitializationService{TContext}"/> pre-warms the key ring during application startup (<c>IHostedService.StartAsync</c>).
/// </para>
/// <para>
/// <b>Atomic Hot-Reload:</b> <see cref="ReloadAsync"/> atomically replaces the inner protector with a freshly loaded key ring
/// and safely disposes the old instance without dropping or blocking in-flight cryptographic operations.
/// </para>
/// <para>
/// <b>Thread Safety:</b> The protector reference is volatile and concurrent reloads are serialized via a <see cref="SemaphoreSlim"/>.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The phantom type representing the secret domain context.</typeparam>
public sealed class ManagedSecretProtector<TContext> : ISecretProtector<TContext>, IDisposable, IAsyncDisposable
    where TContext : ISecretContext {

    private volatile AsyncLazy<SecretProtector<TContext>> _lazy;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly DisposeState _disposeState = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedSecretProtector{TContext}"/> class.
    /// </summary>
    /// <param name="lazy">The lazy evaluator that produces the inner <see cref="SecretProtector{TContext}"/>.</param>
    /// <param name="scopeFactory">The service scope factory used to resolve scoped loaders during reload.</param>
    public ManagedSecretProtector(
        AsyncLazy<SecretProtector<TContext>> lazy,
        IServiceScopeFactory scopeFactory) {
        this._lazy = lazy;
        this._scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Gets a value indicating whether the key ring has been successfully loaded at least once.
    /// </summary>
    public bool IsInitialized => this._lazy.IsValueCreated;

    /// <summary>
    /// Ensures the inner protector is fully initialized. Safe to invoke multiple times.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for initialization.</param>
    /// <returns>A <see cref="ValueTask"/> representing the initialization operation.</returns>
    public async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default) {
        await this._lazy.GetValueAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public KeyVersion CurrentKeyVersion => this.Inner.CurrentKeyVersion;

    /// <inheritdoc/>
    public EncryptedSecret<TContext> Protect(ReadOnlySpan<byte> plainSecret) {
        return this.Inner.Protect(plainSecret);
    }

    /// <inheritdoc/>
    public EncryptedSecret<TContext> Protect(string plainText) {
        return this.Inner.Protect(plainText);
    }

    /// <inheritdoc/>
    public Secret<byte> Unprotect(in EncryptedSecret<TContext> encrypted) {
        return this.Inner.Unprotect(encrypted);
    }

    /// <inheritdoc/>
    public bool NeedsRotation(in EncryptedSecret<TContext> encrypted) {
        return this.Inner.NeedsRotation(encrypted);
    }

    /// <inheritdoc/>
    public EncryptedSecret<TContext> Rotate(in EncryptedSecret<TContext> encrypted) {
        return this.Inner.Rotate(encrypted);
    }

    /// <summary>
    /// Reloads the <see cref="KeyRing{TContext}"/> from the store and atomically replaces the active protector instance.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the reload operation to complete.</param>
    /// <returns>A task representing the asynchronous reload operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this protector has been disposed.</exception>
    public async Task ReloadAsync(CancellationToken cancellationToken = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(ManagedSecretProtector<TContext>));

        await this._reloadLock.WaitAsync(cancellationToken);
        try {
            IServiceScopeFactory scopeFactory = this._scopeFactory;

            AsyncLazy<SecretProtector<TContext>> newLazy = new(async innerCt => {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                KeyRingLoader<TContext> loader =
                    scope.ServiceProvider.GetRequiredService<KeyRingLoader<TContext>>();
                KeyRing<TContext> ring = await loader.LoadAsync(innerCt);
                return new SecretProtector<TContext>(ring);
            });

            // Pre-warm before atomic reference swap
            await newLazy.GetValueAsync(cancellationToken);

            AsyncLazy<SecretProtector<TContext>> old = this._lazy;
            this._lazy = newLazy;

            await old.DisposeAsync();
        }
        finally {
            this._reloadLock.Release();
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Performs asynchronous disposal of the protector and its key ring resources.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the disposal operation.</returns>
    public async ValueTask DisposeAsync() {
        if(this._disposeState.TryBeginDispose()) {
            try {
                await this._lazy.DisposeAsync();
                this._reloadLock.Dispose();
            }
            finally {
                this._disposeState.SetDisposed();
            }
        }
        else {
            await this._disposeState.WaitForDisposedAsync();
        }
    }

    /// <summary>
    /// Synchronously disposes the protector and securely clears active key ring resources.
    /// </summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            try {
                DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            finally {
                this._disposeState.SetDisposed();
            }
        }
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private SecretProtector<TContext> Inner {
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(ManagedSecretProtector<TContext>));
            return this._lazy.GetValueAsync().GetAwaiter().GetResult();
        }
    }
}