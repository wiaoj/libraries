namespace Wiaoj.Identifiers;

/// <summary>Base62 with the alphabet <c>0-9A-Za-z</c>, in the two shapes the codecs need.</summary>
internal static class Base62 {
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>62^11 exceeds 2^64, so an unsigned 64-bit value needs at most 11 digits.</summary>
    public const int MaxUInt64Length = 11;

    /// <summary>62^22 exceeds 2^128 (and 62^21 does not), so a 128-bit value is always written as 22 digits.</summary>
    public const int UInt128Length = 22;

    /// <summary>Writes <paramref name="value"/> with no leading zeros — one spelling per value.</summary>
    public static int WriteUInt64(ulong value, Span<char> destination) {
        Span<char> digits = stackalloc char[MaxUInt64Length];
        int count = 0;
        do {
            (value, ulong remainder) = Math.DivRem(value, 62UL);
            digits[MaxUInt64Length - 1 - count++] = Alphabet[(int)remainder];
        } while(value != 0);

        digits[(MaxUInt64Length - count)..].CopyTo(destination);
        return count;
    }

    /// <summary>Reads a value written by <see cref="WriteUInt64"/>, refusing leading zeros and overflow.</summary>
    public static bool TryReadUInt64(ReadOnlySpan<char> text, out ulong value) {
        value = 0;
        if(text.IsEmpty || text.Length > MaxUInt64Length || (text.Length > 1 && text[0] == '0')) {
            return false;
        }

        foreach(char c in text) {
            int digit = Digit(c);
            if(digit < 0 || value > (ulong.MaxValue - (ulong)digit) / 62UL) {
                value = 0;
                return false;
            }

            value = (value * 62UL) + (ulong)digit;
        }

        return true;
    }

    /// <summary>Writes <paramref name="value"/> as exactly <see cref="UInt128Length"/> digits, zero-padded.</summary>
    public static void WriteUInt128(UInt128 value, Span<char> destination) {
        for(int i = UInt128Length - 1; i >= 0; i--) {
            (value, UInt128 remainder) = UInt128.DivRem(value, 62);
            destination[i] = Alphabet[(int)(ulong)remainder];
        }
    }

    /// <summary>Reads exactly <see cref="UInt128Length"/> digits, refusing any other length and overflow.</summary>
    public static bool TryReadUInt128(ReadOnlySpan<char> text, out UInt128 value) {
        value = UInt128.Zero;
        if(text.Length != UInt128Length) {
            return false;
        }

        foreach(char c in text) {
            int digit = Digit(c);
            if(digit < 0 || value > (UInt128.MaxValue - (UInt128)digit) / 62) {
                value = UInt128.Zero;
                return false;
            }

            value = (value * 62) + (UInt128)digit;
        }

        return true;
    }

    private static int Digit(char c) => c switch {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'Z' => c - 'A' + 10,
        >= 'a' and <= 'z' => c - 'a' + 36,
        _ => -1
    };
}
