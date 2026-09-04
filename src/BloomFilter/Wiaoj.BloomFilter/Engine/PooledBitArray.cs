using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wiaoj.Concurrency;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.BloomFilter.Engine;

/// <summary>
/// Thread-safe bit array implementation backed by pooled memory arrays.
/// Provides atomic bit manipulations and fast bit count operations.
/// </summary>
internal sealed class PooledBitArray : IDisposable {
    private ulong[] _array;
    private readonly DisposeState _disposeState;

    /// <summary>
    /// Gets the total number of bits managed by this array.
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="PooledBitArray"/> with the specified bit capacity.
    /// </summary>
    /// <param name="length">The total number of bits required.</param>
    public PooledBitArray(long length) {
        Preca.ThrowIfNegativeOrZero(length);

        this.Length = length;
        int arraySize = BloomMath.BitsToWordCount(length);
        this._array = ArrayPool<ulong>.Shared.Rent(arraySize);
        Array.Clear(this._array, 0, arraySize);
        this._disposeState = new DisposeState();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(PooledBitArray));
    }

    [DoesNotReturn]
    private static void ThrowIndexOutOfRange() => throw new IndexOutOfRangeException("Bit index was outside the bounds of the bit array.");

    /// <summary>
    /// Atomically sets the bit at the specified index to 1.
    /// </summary>
    /// <param name="index">The zero-based bit index to set.</param>
    /// <returns><see langword="true"/> if the bit was changed from 0 to 1; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Set(long index) {
        ThrowIfDisposed();
        if((ulong)index >= (ulong)this.Length) {
            ThrowIndexOutOfRange();
        }

        long wordIndex = index >> 6;
        int bitIndex = (int)(index & 63);
        ulong mask = 1UL << bitIndex;

        ulong current = Atomic.Read(ref this._array[wordIndex]);
        if((current & mask) != 0) {
            return false;
        }

        ulong original = Interlocked.Or(ref this._array[wordIndex], mask);
        return (original & mask) == 0;
    }

    /// <summary>
    /// Gets the boolean value of the bit at the specified index.
    /// </summary>
    /// <param name="index">The zero-based bit index to read.</param>
    /// <returns><see langword="true"/> if the bit is 1; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(long index) {
        ThrowIfDisposed();
        if((ulong)index >= (ulong)this.Length) {
            ThrowIndexOutOfRange();
        }

        long wordIndex = index >> 6;
        int bitIndex = (int)(index & 63);
        ulong word = Atomic.Read(ref this._array[wordIndex]);
        return (word & (1UL << bitIndex)) != 0;
    }

    /// <summary>
    /// Asynchronously writes the active bytes of the bit array to a destination stream.
    /// </summary>
    /// <param name="destination">The stream to write data into.</param>
    /// <param name="ct">The cancellation token.</param>
    public async ValueTask WriteToStreamAsync(Stream destination, CancellationToken ct) {
        ThrowIfDisposed();
        int byteCount = (int)BloomMath.BitsToBytes(this.Length);
        using UlongToByteMemoryManager manager = new(this._array);
        Memory<byte> memory = manager.Memory;

        await destination.WriteAsync(memory[..byteCount], ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronously writes the active bytes of the bit array to a destination stream.
    /// </summary>
    /// <param name="destination">The stream to write data into.</param>
    public void WriteToStream(Stream destination) {
        ThrowIfDisposed();
        int byteCount = (int)BloomMath.BitsToBytes(this.Length);
        ReadOnlySpan<byte> byteSpan = MemoryMarshal.AsBytes(this._array.AsSpan());
        destination.Write(byteSpan[..byteCount]);
    }

    /// <summary>
    /// Calculates the 64-bit XXHash3 checksum over the active bytes of the array.
    /// </summary>
    /// <returns>A 64-bit unsigned checksum integer.</returns>
    public ulong CalculateChecksum() {
        ThrowIfDisposed();
        Span<byte> byteSpan = MemoryMarshal.AsBytes(this._array.AsSpan());
        int byteCount = (int)BloomMath.BitsToBytes(this.Length);
        return XxHash3.Compute(byteSpan[..byteCount]).Value;
    }

    /// <summary>
    /// Asynchronously reads bytes from a stream into the bit array and verifies the checksum.
    /// </summary>
    /// <param name="source">The source stream to read from.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The calculated checksum of the loaded data.</returns>
    public async ValueTask<ulong> LoadFromStreamAsync(Stream source, CancellationToken ct) {
        ThrowIfDisposed();
        using UlongToByteMemoryManager manager = new(this._array);
        Memory<byte> buffer = manager.Memory;

        int bytesToRead = (int)BloomMath.BitsToBytes(this.Length);
        Memory<byte> target = buffer[..bytesToRead];

        int totalRead = 0;
        while(totalRead < bytesToRead) {
            int read = await source.ReadAsync(target[totalRead..], ct).ConfigureAwait(false);
            if(read == 0) {
                break;
            }
            totalRead += read;
        }

        return XxHash3.Compute(target.Span).Value;
    }

    /// <summary>
    /// Counts the total number of bits currently set to 1 using CPU population count instructions.
    /// </summary>
    /// <returns>The total number of set bits.</returns>
    public long GetPopCount() {
        ThrowIfDisposed();
        int wordCount = BloomMath.BitsToWordCount(this.Length);
        long count = 0;

        for(int i = 0; i < wordCount; i++) {
            count += BitOperations.PopCount(Atomic.Read(ref this._array[i]));
        }

        return count;
    }

    /// <summary>
    /// Returns the underlying buffer to the shared array pool.
    /// </summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            ulong[]? array = Atomic.Exchange(ref this._array, null!);
            if(array != null) {
                ArrayPool<ulong>.Shared.Return(array);
            }
            this._disposeState.SetDisposed();
        }
    }

    private sealed class UlongToByteMemoryManager(ulong[] array) : MemoryManager<byte> {
        public override Span<byte> GetSpan() {
            return MemoryMarshal.AsBytes(array.AsSpan());
        }

        public override unsafe MemoryHandle Pin(int elementIndex = 0) {
            GCHandle handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            void* pointer = (void*)handle.AddrOfPinnedObject();
            byte* offsetPointer = (byte*)pointer + elementIndex;
            return new MemoryHandle(offsetPointer, handle, this);
        }

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }
}