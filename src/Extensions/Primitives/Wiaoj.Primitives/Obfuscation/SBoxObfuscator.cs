using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wiaoj.Primitives.Obfuscation;
/// <summary>
/// Provides a high-performance obfuscation strategy using a Substitution Box (S-Box) and bitwise circular rotation (Permutation).
/// Optimized for zero-allocation and SIMD-friendly manual rotation.
/// </summary>
public sealed class SBoxObfuscator : IObfuscator {
    // Statik alphabet tanımı heap allocation'ı engeller.
    private static ReadOnlySpan<char> Alphabet => "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private readonly byte[] _sbox = new byte[256];
    private readonly byte[] _invSbox = new byte[256];

    /// <summary>
    /// Initializes a new instance of the <see cref="SBoxObfuscator"/> class with a custom seed for S-Box generation.
    /// </summary>
    /// <param name="seed">The seed value used to shuffle the substitution table.</param>
    public SBoxObfuscator(byte seed) {
        // S-Box oluşturma mantığı (Fisher-Yates varyasyonu)
        for(int i = 0; i < 256; i++) this._sbox[i] = (byte)i;

        byte j = 0;
        for(int i = 0; i < 256; i++) {
            j = (byte)((j + this._sbox[i] + seed) % 256);
            (this._sbox[i], this._sbox[j]) = (this._sbox[j], this._sbox[i]);
        }

        for(int i = 0; i < 256; i++) this._invSbox[this._sbox[i]] = (byte)i;
    }

    /// <inheritdoc/>
    public bool TryEncode(Int128 value, Span<char> destination, out int charsWritten) {
        // Değeri doğrudan byte span olarak manipüle et
        Span<byte> bytes = stackalloc byte[16];
        MemoryMarshal.TryWrite(bytes, in value);

        // 1. Substitution (Yerine koyma)
        for(int i = 0; i < 16; i++) {
            bytes[i] = this._sbox[bytes[i]];
        }

        // 2. Permutation (128-bit Bitwise Rotation)
        UInt128 v = MemoryMarshal.Read<UInt128>(bytes);
        UInt128 scrambled = (v << 47) | (v >> (128 - 47));

        // 3. Base62 Encode
        return EncodeBase62(scrambled, destination, out charsWritten);
    }

    /// <inheritdoc/>
    public bool TryDecode(ReadOnlySpan<char> payload, out Int128 result) {
        if(!TryParseBase62(payload, out UInt128 v)) {
            result = 0;
            return false;
        }

        // 1. Reverse Permutation
        UInt128 descrambled = (v >> 47) | (v << (128 - 47));

        Span<byte> bytes = stackalloc byte[16];
        MemoryMarshal.TryWrite(bytes, in descrambled);

        // 2. Reverse Substitution
        for(int i = 0; i < 16; i++) {
            bytes[i] = this._invSbox[bytes[i]];
        }

        result = MemoryMarshal.Read<Int128>(bytes);
        return true;
    }

    /// <inheritdoc/>
    public bool TryDecodeUtf8(ReadOnlySpan<byte> payload, out Int128 result) {
        // UTF-8 byte'ları üzerinden doğrudan Base62 çözme (Allocation yok)
        if(!TryParseBase62Utf8(payload, out UInt128 v)) {
            result = 0;
            return false;
        }

        UInt128 descrambled = (v >> 47) | (v << (128 - 47));
        Span<byte> bytes = stackalloc byte[16];
        MemoryMarshal.TryWrite(bytes, in descrambled);

        for(int i = 0; i < 16; i++) {
            bytes[i] = this._invSbox[bytes[i]];
        }

        result = MemoryMarshal.Read<Int128>(bytes);
        return true;
    }

    /// <inheritdoc/>
    public void EncodeTo(Int128 value, IBufferWriter<char> writer) {
        Span<char> span = writer.GetSpan(32);
        if(TryEncode(value, span, out int w)) {
            writer.Advance(w);
        }
    }

    #region Helpers

    private static bool EncodeBase62(UInt128 v, Span<char> dest, out int written) {
        int i = 0;
        Span<char> buf = stackalloc char[32];
        ReadOnlySpan<char> alphabet = Alphabet;

        do {
            (v, UInt128 rem) = UInt128.DivRem(v, 62);
            buf[i++] = alphabet[(int)rem];
        } while(v > 0);

        if(dest.Length < i) {
            written = 0;
            return false;
        }

        for(int j = 0; j < i; j++) {
            dest[j] = buf[i - 1 - j];
        }

        written = i;
        return true;
    }

    private static bool TryParseBase62(ReadOnlySpan<char> s, out UInt128 v) {
        v = 0;
        foreach(char c in s) {
            int val = DecodeChar(c);
            if(val == -1) return false;
            try { checked { v = (v * 62) + (uint)val; } } catch { return false; }
        }
        return true;
    }

    private static bool TryParseBase62Utf8(ReadOnlySpan<byte> s, out UInt128 v) {
        v = 0;
        foreach(byte b in s) {
            int val = DecodeChar((char)b);
            if(val == -1) return false;
            try { checked { v = (v * 62) + (uint)val; } } catch { return false; }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DecodeChar(char c) {
        return c switch {
            >= '0' and <= '9' => c - '0',
            >= 'A' and <= 'Z' => c - 'A' + 10,
            >= 'a' and <= 'z' => c - 'a' + 36,
            _ => -1
        };
    }

    #endregion
}