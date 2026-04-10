using System.Buffers;

namespace Wiaoj.Primitives.Obfuscation;

/// <summary>
/// Provides a high-performance, zero-allocation obfuscator that uses a Feistel Cipher for scrambling 
/// and Base62 encoding for string representation.
/// </summary>
public sealed class FeistelBase62Obfuscator : IObfuscator, IDisposable {
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"; 
    private readonly Secret<uint> _keys;


    /// <summary>
    /// Initializes a new instance of the <see cref="FeistelBase62Obfuscator"/> class using the specified options.
    /// </summary>
    /// <param name="options">The configuration options including the seed used for key derivation.</param>
    public FeistelBase62Obfuscator(FeistelObfuscatorOptions options) {
        Preca.ThrowIfNull(options);
        uint[] tempKeys = FeistelCipher.DeriveKeys(options.Seed.Span);
        this._keys = Secret.From(tempKeys);
    }

    /// <summary>
    /// Encodes a 128-bit integer into an obfuscated Base62 string representation without heap allocations.
    /// </summary>
    /// <param name="value">The 128-bit integer value to obfuscate and encode.</param>
    /// <param name="destination">The span to write the resulting Base62 characters into.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written to the destination.</param>
    /// <returns><see langword="true"/> if the encoding was successful and the destination was large enough; otherwise, <see langword="false"/>.</returns>
    public bool TryEncode(Int128 value, Span<char> destination, out int charsWritten) {
        if(value == 0) {
            if(destination.Length < 1) { charsWritten = 0; return false; }
            destination[0] = Alphabet[0];
            charsWritten = 1;
            return true;
        }

        // Expose kullanarak hem anahtara güvenli eriş, hem de TryEncode'un sonucunu dön
        (Int128 scrambled, bool success) = this._keys.Expose(keysSpan => {
            Int128 s = ((value >> 64) == 0)
                ? (Int128)FeistelCipher.Encrypt64((ulong)value, keysSpan)
                : FeistelCipher.Encrypt128(value, keysSpan);
            return (s, true);
        });

        if(!success) { charsWritten = 0; return false; }

        // Base62 Encode
        UInt128 v = (UInt128)scrambled;
        int i = 0;
        Span<char> buffer = stackalloc char[32];
        while(v > 0) {
            (v, UInt128 rem) = UInt128.DivRem(v, 62);
            buffer[i++] = Alphabet[(int)rem];
        }

        if(destination.Length < i) { charsWritten = 0; return false; }
        for(int j = 0; j < i; j++) destination[j] = buffer[i - 1 - j];

        charsWritten = i;
        return true;
    }

    /// <summary>
    /// Decodes an obfuscated Base62 character span back into its original 128-bit integer representation.
    /// </summary>
    /// <param name="payload">The ReadOnlySpan containing the Base62 encoded characters.</param>
    /// <param name="result">When this method returns, contains the decoded 128-bit integer.</param>
    /// <returns><see langword="true"/> if the decoding and de-obfuscation were successful; otherwise, <see langword="false"/>.</returns>
    public bool TryDecode(ReadOnlySpan<char> payload, out Int128 result) {
        // 1. Base62 Decode
        UInt128 v = 0;
        foreach(char c in payload) {
            int val = c switch {
                >= '0' and <= '9' => c - '0',
                >= 'A' and <= 'Z' => c - 'A' + 10,
                >= 'a' and <= 'z' => c - 'a' + 36,
                _ => -1
            };
            if(val == -1) { result = 0; return false; }
            try { checked { v = (v * 62) + (uint)val; } }
            catch(OverflowException) { result = 0; return false; }
        }

        // 2. Descramble (Deobfuscate) - Secret içindeki anahtarı güvenle kullan
        result = this._keys.Expose(keysSpan => {
            Int128 scrambled = (Int128)v;
            return (scrambled >> 64) == 0
                ? (Int128)FeistelCipher.Decrypt64((ulong)scrambled, keysSpan)
                : FeistelCipher.Decrypt128(scrambled, keysSpan);
        });

        return true;
    }

    /// <summary>
    /// Decodes an obfuscated Base62 UTF-8 byte span back into its original 128-bit integer representation.
    /// </summary>
    /// <param name="utf8Payload">The ReadOnlySpan containing the Base62 encoded UTF-8 bytes.</param>
    /// <param name="result">When this method returns, contains the decoded 128-bit integer.</param>
    /// <returns><see langword="true"/> if the decoding from UTF-8 and de-obfuscation were successful; otherwise, <see langword="false"/>.</returns>
    public bool TryDecodeUtf8(ReadOnlySpan<byte> utf8Payload, out Int128 result) {
        UInt128 v = 0;
        foreach(byte b in utf8Payload) {
            int val = b switch {
                >= (byte)'0' and <= (byte)'9' => b - '0',
                >= (byte)'A' and <= (byte)'Z' => b - 'A' + 10,
                >= (byte)'a' and <= (byte)'z' => b - 'a' + 36,
                _ => -1
            };
            if(val == -1) { result = 0; return false; }
            try { checked { v = (v * 62) + (uint)val; } }
            catch(OverflowException) { result = 0; return false; }
        }

        result = this._keys.Expose(keysSpan => {
            Int128 scrambled = (Int128)v;
            return (scrambled >> 64) == 0
                ? (Int128)FeistelCipher.Decrypt64((ulong)scrambled, keysSpan)
                : FeistelCipher.Decrypt128(scrambled, keysSpan);
        });

        return true;
    }

    private const int maxChars = 24;
    /// <inheritdoc/>
    public void EncodeTo(Int128 value, IBufferWriter<char> writer) {
        Span<char> span = writer.GetSpan(maxChars);

        if(TryEncode(value, span, out int written)) {
            writer.Advance(written);
        }
        else {
            Span<char> overflowBuffer = stackalloc char[maxChars];
            TryEncode(value, overflowBuffer, out int overflowWritten);
            writer.Write(overflowBuffer[..overflowWritten]);
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        this._keys.Dispose();
    }
}

/// <summary>
/// Configuration options for the <see cref="FeistelBase62Obfuscator"/>.
/// </summary>
public sealed record FeistelObfuscatorOptions {
    /// <summary>
    /// The secret seed bytes used to derive round keys. 
    /// Must be at least 16 bytes for secure key derivation.
    /// </summary>
    public required ReadOnlyMemory<byte> Seed { get; init; }
}