using System.Buffers;

namespace Wiaoj.Primitives.Obfuscation;
/// <summary>
/// Provides a high-performance bit-permutation obfuscator that encodes IDs as a bit-stream string (0s and 1s).
/// Optimized for zero-allocation during encoding and decoding.
/// </summary>
public sealed class BitStreamObfuscator : IObfuscator {
    // Permütasyon tablolarını heap allocation'dan kaçınmak için bir kez constructor'da hazırlıyoruz.
    private readonly int[] _permutationTable;
    private readonly int[] _inverseTable;

    /// <summary>
    /// Initializes a new instance of the <see cref="BitStreamObfuscator"/> class with a specific seed.
    /// </summary>
    /// <param name="seed">The seed used to generate the deterministic bit-permutation table.</param>
    public BitStreamObfuscator(int seed) {
        this._permutationTable = new int[128];
        this._inverseTable = new int[128];

        // LINQ yerine basit döngü ve Random kullanımı (Sadece nesne oluşurken maliyetli)
        int[] indices = new int[128];
        for(int i = 0; i < 128; i++) indices[i] = i;

        Random rng = new(seed);
        // Fisher-Yates shuffle: Verimli ve adil bir karıştırma yöntemi
        for(int i = 127; i > 0; i--) {
            int k = rng.Next(i + 1);
            (indices[i], indices[k]) = (indices[k], indices[i]);
        }

        for(int i = 0; i < 128; i++) {
            this._permutationTable[i] = indices[i];
            this._inverseTable[indices[i]] = i;
        }
    }

    /// <inheritdoc/>
    public bool TryEncode(Int128 value, Span<char> destination, out int charsWritten) {
        if(destination.Length < 128) {
            charsWritten = 0;
            return false;
        }

        // Bitwise operasyonları ve loop unrolling potansiyeli için optimize döngü
        for(int i = 0; i < 128; i++) {
            // i. biti doğrudan maskeleme ile kontrol et
            int bit = (int)((value >> i) & 1);
            destination[this._permutationTable[i]] = (char)('0' + bit);
        }

        charsWritten = 128;
        return true;
    }

    /// <inheritdoc/>
    public bool TryDecode(ReadOnlySpan<char> payload, out Int128 result) {
        if(payload.Length != 128) {
            result = 0;
            return false;
        }

        Int128 decoded = 0;
        // Metot içinde inverseTable oluşturmak yerine sınıf seviyesindeki tabloyu kullan (Zero-Alloc)
        for(int i = 0; i < 128; i++) {
            if(payload[i] == '1') {
                decoded |= ((Int128)1 << this._inverseTable[i]);
            }
            // '0' ise işlem yapmaya gerek yok, bit zaten 0'dır.
        }

        result = decoded;
        return true;
    }

    /// <inheritdoc/>
    public bool TryDecodeUtf8(ReadOnlySpan<byte> utf8Payload, out Int128 result) {
        if(utf8Payload.Length != 128) {
            result = 0;
            return false;
        }

        Int128 decoded = 0;
        // UTF-8 byte'ları üzerinden doğrudan kontrol (stackalloc char maliyetini de sildik)
        for(int i = 0; i < 128; i++) {
            if(utf8Payload[i] == (byte)'1') {
                decoded |= ((Int128)1 << this._inverseTable[i]);
            }
        }

        result = decoded;
        return true;
    }

    /// <inheritdoc/>
    public void EncodeTo(Int128 value, IBufferWriter<char> writer) {
        Span<char> span = writer.GetSpan(128);
        if(TryEncode(value, span, out int written)) {
            writer.Advance(written);
        }
    }
}