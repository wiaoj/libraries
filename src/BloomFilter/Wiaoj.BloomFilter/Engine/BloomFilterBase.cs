using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.BloomFilter.Engine;
/// <summary>
/// Provides a unified base implementation for Bloom Filters, 
/// handling common char-to-byte transcoding and lock-free disposal lifecycle via <see cref="DisposeState"/>.
/// </summary>
internal abstract class BloomFilterBase : IPersistentBloomFilter, IDisposable {
    /// <summary>
    /// Tracks the lock-free disposal state of this filter.
    /// </summary>
    protected readonly DisposeState DisposeState = new();

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract BloomFilterConfiguration Configuration { get; }

    /// <inheritdoc/>
    public abstract bool IsDirty { get; }

    /// <inheritdoc/>
    public abstract bool Add(ReadOnlySpan<byte> item);

    /// <inheritdoc/>
    public abstract bool Contains(ReadOnlySpan<byte> item);

    /// <inheritdoc/>
    public abstract long GetPopCount();

    /// <inheritdoc/>
    public abstract ValueTask SaveAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public abstract ValueTask ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the object state and throws an <see cref="ObjectDisposedException"/> 
    /// if the filter is currently disposing or already disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ThrowIfDisposed() {
        this.DisposeState.ThrowIfDisposingOrDisposed(this.Name);
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<char> item) {
        ThrowIfDisposed();

        if(item.IsEmpty) {
            return Add(ReadOnlySpan<byte>.Empty);
        }

        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        using ValueBuffer<byte> buffer = new(maxBytes, stackalloc byte[256]);

        OperationStatus status = Utf8.FromUtf16(item, buffer.Span, out _, out int bytesWritten);
        return status == OperationStatus.Done && Add(buffer.Slice(0, bytesWritten));
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<char> item) {
        ThrowIfDisposed();

        if(item.IsEmpty) {
            return Contains(ReadOnlySpan<byte>.Empty);
        }

        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        using ValueBuffer<byte> buffer = new(maxBytes, stackalloc byte[256]);

        OperationStatus status = Utf8.FromUtf16(item, buffer.Span, out _, out int bytesWritten);
        return status == OperationStatus.Done && Contains(buffer.Slice(0, bytesWritten));
    }

    /// <summary>
    /// Releases all managed and unmanaged resources in a lock-free, thread-safe manner.
    /// </summary>
    public void Dispose() {
        if(this.DisposeState.TryBeginDispose()) {
            try {
                DisposeCore();
            }
            finally {
                this.DisposeState.SetDisposed();
            }

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Releases concrete filter resources (buffers, locks, child shards/layers).
    /// </summary>
    protected abstract void DisposeCore();
}