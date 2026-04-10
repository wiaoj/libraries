using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives.Obfuscation;
/// <summary>
/// A high-performance obfuscator using XorShift and bitwise Rotate logic for rapid scrambling.
/// This implementation is symmetric and optimized for zero-allocation performance.
/// </summary>
public sealed class XorShiftObfuscator(ulong keyA, ulong keyB) : IObfuscator {
    private readonly ulong _keyA = keyA;
    private readonly ulong _keyB = keyB;

    // Statik Span kullanımı heap allocation'ı önler ve erişim hızını artırır.
    private static ReadOnlySpan<char> Alphabet => "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <inheritdoc/>
    public bool TryEncode(Int128 value, Span<char> destination, out int charsWritten) {
        // 1. Scramble (XOR + Rotate)
        // Değeri düşük (low) ve yüksek (high) 64-bit parçalara ayırıp ayrı anahtarlarla işliyoruz.
        ulong low = (ulong)value;
        ulong high = (ulong)(value >> 64);

        low ^= this._keyA;
        low = BitOperations.RotateLeft(low, 13);
        high ^= this._keyB;
        high = BitOperations.RotateLeft(high, 17);

        UInt128 v = ((UInt128)high << 64) | (UInt128)low;

        // 2. Base62 Encode
        int i = 0;
        Span<char> buffer = stackalloc char[32];
        ReadOnlySpan<char> alphabet = Alphabet;

        do {
            (v, UInt128 rem) = UInt128.DivRem(v, 62);
            buffer[i++] = alphabet[(int)rem];
        } while(v > 0);

        if(destination.Length < i) {
            charsWritten = 0;
            return false;
        }

        // Buffer'daki ters dizilmiş karakterleri düzeltilerek hedefe yazılması
        for(int j = 0; j < i; j++) {
            destination[j] = buffer[i - 1 - j];
        }

        charsWritten = i;
        return true;
    }

    /// <inheritdoc/>
    public bool TryDecode(ReadOnlySpan<char> payload, out Int128 result) {
        if(!TryParseBase62(payload, out UInt128 v)) {
            result = 0;
            return false;
        }

        ulong low = (ulong)v;
        ulong high = (ulong)(v >> 64);

        // 2. Reverse Scramble (RotateRight & XOR işlemlerini ters sırayla uyguluyoruz)
        high = BitOperations.RotateRight(high, 17);
        high ^= this._keyB;
        low = BitOperations.RotateRight(low, 13);
        low ^= this._keyA;

        result = ((Int128)high << 64) | (Int128)low;
        return true;
    }

    /// <inheritdoc/>
    public bool TryDecodeUtf8(ReadOnlySpan<byte> utf8Payload, out Int128 result) {
        // UTF-8 byte'ları üzerinden doğrudan Base62 çözme (Allocation yok)
        if(!TryParseBase62Utf8(utf8Payload, out UInt128 v)) {
            result = 0;
            return false;
        }

        ulong low = (ulong)v;
        ulong high = (ulong)(v >> 64);

        high = BitOperations.RotateRight(high, 17);
        high ^= this._keyB;
        low = BitOperations.RotateRight(low, 13);
        low ^= this._keyA;

        result = ((Int128)high << 64) | (Int128)low;
        return true;
    }

    /// <inheritdoc/>
    public void EncodeTo(Int128 value, IBufferWriter<char> writer) {
        // 32 karakterlik bir alan iste (UInt128 için Base62 sınırı 22-24 karakterdir)
        Span<char> span = writer.GetSpan(32);
        if(TryEncode(value, span, out int charsWritten)) {
            writer.Advance(charsWritten);
        }
    }

    #region Helpers

    private static bool TryParseBase62(ReadOnlySpan<char> source, out UInt128 value) {
        value = 0;
        foreach(char c in source) {
            int v = DecodeChar(c);
            if(v == -1) return false;
            try { checked { value = (value * 62) + (uint)v; } } catch { return false; }
        }
        return true;
    }

    private static bool TryParseBase62Utf8(ReadOnlySpan<byte> source, out UInt128 value) {
        value = 0;
        foreach(byte b in source) {
            // Byte'tan char'a cast maliyetsizdir (ASCII bazlı Base62 için)
            int v = DecodeChar((char)b);
            if(v == -1) return false;
            try { checked { value = (value * 62) + (uint)v; } } catch { return false; }
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