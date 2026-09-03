using System.Buffers;
using System.IO.Hashing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Internal;

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
        this.Length = length;
        int arraySize = (int)((length + 63) / 64);
        this._array = ArrayPool<ulong>.Shared.Rent(arraySize);
        Array.Clear(this._array, 0, arraySize);
        this._disposeState = new DisposeState();
    }

    /// <summary>
    /// Atomically sets the bit at the specified index to 1.
    /// </summary>
    /// <param name="index">The zero-based bit index to set.</param>
    /// <returns><see langword="true"/> if the bit was changed from 0 to 1; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Set(long index) {
        long wordIndex = index >> 6;
        int bitIndex = (int)(index & 63);
        ulong mask = 1UL << bitIndex;

        ulong current = Volatile.Read(ref this._array[wordIndex]);
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
        long wordIndex = index >> 6;
        int bitIndex = (int)(index & 63);
        ulong word = Volatile.Read(ref this._array[wordIndex]);
        return (word & (1UL << bitIndex)) != 0;
    }

    /// <summary>
    /// Asynchronously writes the active bytes of the bit array to a destination stream.
    /// </summary>
    /// <param name="destination">The stream to write data into.</param>
    /// <param name="ct">The cancellation token.</param>
    public async ValueTask WriteToStreamAsync(Stream destination, CancellationToken ct) {
        int byteCount = (int)((this.Length + 7) / 8);
        using UlongToByteMemoryManager manager = new(this._array);
        Memory<byte> memory = manager.Memory;

        await destination.WriteAsync(memory[..byteCount], ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Calculates the 64-bit XXHash3 checksum over the active bytes of the array.
    /// </summary>
    /// <returns>A 64-bit unsigned checksum integer.</returns>
    public ulong CalculateChecksum() {
        Span<byte> byteSpan = MemoryMarshal.AsBytes(this._array.AsSpan());
        int byteCount = (int)((this.Length + 7) / 8);
        return XxHash3.HashToUInt64(byteSpan[..byteCount]);
    }

    /// <summary>
    /// Asynchronously reads bytes from a stream into the bit array and verifies the checksum.
    /// </summary>
    /// <param name="source">The source stream to read from.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The calculated checksum of the loaded data.</returns>
    public async ValueTask<ulong> LoadFromStreamAsync(Stream source, CancellationToken ct) {
        using UlongToByteMemoryManager manager = new(this._array);
        Memory<byte> buffer = manager.Memory;

        int bytesToRead = (int)((this.Length + 7) / 8);
        Memory<byte> target = buffer[..bytesToRead];

        int totalRead = 0;
        while(totalRead < bytesToRead) {
            int read = await source.ReadAsync(target[totalRead..], ct).ConfigureAwait(false);
            if(read == 0) {
                break;
            }
            totalRead += read;
        }

        return XxHash3.HashToUInt64(target.Span);
    }

    /// <summary>
    /// Counts the total number of bits currently set to 1 using CPU population count instructions.
    /// </summary>
    /// <returns>The total number of set bits.</returns>
    public long GetPopCount() {
        int wordCount = (int)((this.Length + 63) / 64);
        long count = 0;

        for(int i = 0; i < wordCount; i++) {
            count += BitOperations.PopCount(Volatile.Read(ref this._array[i]));
        }

        return count;
    }

    /// <summary>
    /// Returns the underlying buffer to the shared array pool.
    /// </summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            ulong[]? array = Interlocked.Exchange(ref this._array, null!);
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