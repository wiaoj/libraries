using System.Buffers;
using System.IO.Hashing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Internal;
/// <summary>
/// A thread-safe, high-performance bit array implementation that uses pooled memory to reduce GC pressure.
/// Supports atomic bit setting operations.
/// </summary>
internal sealed class PooledBitArray : IDisposable {
    private ulong[] _array;
    private readonly DisposeState _disposeState;

    /// <summary>
    /// Gets the total number of bits in the array.
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// Initializes a new empty instance of <see cref="PooledBitArray"/> with the specified length.
    /// </summary>
    public PooledBitArray(long length) {
        this.Length = length;
        int arraySize = (int)((length + 63) / 64);
        this._array = ArrayPool<ulong>.Shared.Rent(arraySize);
        Array.Clear(this._array, 0, arraySize);
        this._disposeState = new DisposeState();
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PooledBitArray"/> by copying data from a byte buffer.
    /// </summary>
    public PooledBitArray(byte[] bytes, long length) {
        this.Length = length;
        int arraySize = (int)((length + 63) / 64);
        this._array = ArrayPool<ulong>.Shared.Rent(arraySize);

        // Copy bytes to ulong array
        int bytesToCopy = Math.Min(bytes.Length, arraySize * 8);
        Buffer.BlockCopy(bytes, 0, this._array, 0, bytesToCopy);

        // Zero out any remaining padding in the last ulong if necessary
        int copiedLongs = (bytesToCopy + 7) / 8;
        if(copiedLongs < arraySize) {
            Array.Clear(this._array, copiedLongs, arraySize - copiedLongs);
        }
        this._disposeState = new DisposeState();
    }

    /// <summary>
    /// Atomically sets the bit at the specified index to 1.
    /// </summary>
    /// <returns><c>true</c> if the bit was changed from 0 to 1; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Set(long index) {
        long wordIndex = index >> 6; // index / 64
        int bitIndex = (int)(index & 63); // index % 64
        ulong mask = 1UL << bitIndex;

        ulong current = Volatile.Read(ref this._array[wordIndex]);
        if((current & mask) != 0) return false;

        // Değişiklik gerekliyse atomik işlem yap
        ulong original = Interlocked.Or(ref this._array[wordIndex], mask);
        return (original & mask) == 0;
    }

    /// <summary>
    /// Gets the value of the bit at the specified index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(long index) {
        long wordIndex = index >> 6;
        int bitIndex = (int)(index & 63);
        ulong word = Volatile.Read(ref this._array[wordIndex]);
        return (word & (1UL << bitIndex)) != 0;
    }

    /// <summary>
    /// Copies the underlying bits to a destination byte array.
    /// </summary>
    public void CopyTo(byte[] destination) {
        int byteCount = (int)((this.Length + 7) / 8);
        if(destination.Length < byteCount) throw new ArgumentException("Destination array is too small.");
        Buffer.BlockCopy(this._array, 0, destination, 0, byteCount);
    }

    public async ValueTask WriteToStreamAsync(Stream destination, CancellationToken ct) {
        // Asıl veri boyutu (Padding hariç)
        int byteCount = (int)((this.Length + 7) / 8);

        // MemoryManager kullanarak allocation yapmadan yaz
        using UlongToByteMemoryManager manager = new(this._array);
        Memory<byte> memory = manager.Memory;

        await destination.WriteAsync(memory[..byteCount], ct);
    }


    public ulong CalculateChecksum() {
        Span<byte> byteSpan = MemoryMarshal.AsBytes(this._array.AsSpan());
        int byteCount = (int)((this.Length + 7) / 8);
        return XxHash3.HashToUInt64(byteSpan[..byteCount]);
    }

    public async ValueTask<ulong> LoadFromStreamAsync(Stream source, CancellationToken ct) {
        using UlongToByteMemoryManager manager = new(this._array);
        Memory<byte> buffer = manager.Memory;

        int bytesToRead = (int)((this.Length + 7) / 8);
        Memory<byte> target = buffer[0..bytesToRead];

        int totalRead = 0;
        while(totalRead < bytesToRead) {
            // Artik hata vermez çünkü 'target' bir Memory<byte>'dır!
            int read = await source.ReadAsync(target[totalRead..], ct);
            if(read == 0) break;
            totalRead += read;
        }

        return XxHash3.HashToUInt64(target.Span);
    }

    public long GetPopCount() {
        // Pool'dan gelen dizinin tamamını değil, sadece bizim kullandığımız kısmını saymalıyız.
        int wordCount = (int)((this.Length + 63) / 64);
        long count = 0;

        for(int i = 0; i < wordCount; i++) {
            // Bu metod CPU'daki POPCNT komutunu kullanır, inanılmaz hızlıdır.
            count += BitOperations.PopCount(this._array[i]);
        }

        return count;
    }

    /// <summary>
    /// Returns the underlying array to the pool.
    /// </summary>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            if(this._array != null) {
                ArrayPool<ulong>.Shared.Return(this._array);
                this._array = null!;
            }
            this._disposeState.SetDisposed();
        }
    }

    private sealed class UlongToByteMemoryManager(ulong[] array) : MemoryManager<byte> {
        public override Span<byte> GetSpan() {
            return MemoryMarshal.AsBytes(array.AsSpan());
        }

        // Belleği sabitlemek isteyen olursa burası çalışır
        public override unsafe MemoryHandle Pin(int elementIndex = 0) {
            // GCHandle ile diziyi belleğe çiviliyoruz
            GCHandle handle = GCHandle.Alloc(array, GCHandleType.Pinned);

            // Sabitlenmiş adresin pointer'ını hesaplıyoruz
            void* pointer = (void*)handle.AddrOfPinnedObject();

            // Byte bazlı offset ekliyoruz
            byte* offsetPointer = (byte*)pointer + elementIndex;

            // handle'ı da MemoryHandle içine veriyoruz ki Unpin dendiğinde free edilebilsin
            return new MemoryHandle(offsetPointer, handle, this);
        }

        // MemoryHandle doğrudan GCHandle'ı yönettiği için burası artık güvenle boş kalabilir.
        public override void Unpin() { }

        // Dizi Pool'dan geldiği için burada diziyi dispose etmiyoruz (zaten Pool yönetecek).
        // Sadece manager'ın kendisiyle ilgili bir temizlik varsa yapılır.
        protected override void Dispose(bool disposing) { }
    }
}