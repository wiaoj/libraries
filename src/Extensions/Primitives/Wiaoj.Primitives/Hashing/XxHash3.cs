using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives.Hashing;

/// <summary>
/// Represents an 8-byte (64-bit) XXHash3 hash. This struct is allocation-free and
/// uses a plain <see langword="ulong"/> field — no <see langword="unsafe"/> or
/// <see langword="fixed"/> buffer required.
/// Non-cryptographic. Use for checksums, deduplication, and cache-key generation.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(XxHash3JsonConverter))]
public readonly struct XxHash3
    : IEquatable<XxHash3>,
      ISpanFormattable,
      IUtf8SpanFormattable,
      IEqualityOperators<XxHash3, XxHash3, bool> {

    // -------------------------------------------------------------------------
    // Algorithm constants
    // -------------------------------------------------------------------------
    internal const int HashSizeInBytes = 8;

    private const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
    private const ulong Prime64_2 = 0xC2B2AE3D27D4EB4FUL;
    private const ulong Prime64_3 = 0x165667B19E3779F9UL;
    private const ulong Prime64_4 = 0x85EBCA77C2B2AE63UL;
    private const ulong Prime64_5 = 0x27D4EB2F165667C5UL;
    private const uint Prime32_1 = 0x9E3779B1U;
    private const uint Prime32_2 = 0x85EBCA77U;
    private const uint Prime32_3 = 0xC2B2AE3DU;

    private const int NbAccs = 8;
    private const int StripLen = 64;
    private const int BlockLen = 1024; // StripLen * NbStripesPerBlock
    private const int NbStripesPerBlock = 16;   // (SecretLen - StripLen) / 8 = (192-64)/8
    private const int MidSizeStartOffset = 3;
    private const int MidSizeLastOffset = 17;
    private const int SecretMergeAccsStart = 11;

    // 192-byte default secret (same as reference implementation).
    // Returned as ReadOnlySpan<byte> so the JIT stores it in the read-only data
    // section — zero heap allocation per call.
    private static ReadOnlySpan<byte> DefaultSecret => [
        0xb8, 0xfe, 0x6c, 0x39, 0x23, 0xa4, 0x4b, 0xbe, 0x7c, 0x01, 0x81, 0x2c, 0xf7, 0x21, 0xad, 0x1c,
        0xde, 0xd4, 0x6d, 0xe9, 0x83, 0x90, 0x97, 0xdb, 0x72, 0x40, 0xa4, 0xa4, 0xb7, 0xb3, 0x67, 0x1f,
        0xcb, 0x79, 0xe6, 0x4e, 0xcc, 0xc0, 0xe5, 0x78, 0x82, 0x5a, 0xd0, 0x7d, 0xcc, 0xff, 0x72, 0x21,
        0xb8, 0x08, 0x46, 0x74, 0xf7, 0x43, 0x24, 0x8e, 0xe0, 0x35, 0x90, 0xe6, 0x81, 0x3a, 0x26, 0x4c,
        0x3c, 0x28, 0x52, 0xbb, 0x91, 0xc3, 0x00, 0xcb, 0x88, 0xd0, 0x65, 0x8b, 0x1b, 0x53, 0x2e, 0xa3,
        0x71, 0x64, 0x48, 0x97, 0xa2, 0x0d, 0xf9, 0x4e, 0x38, 0x19, 0xef, 0x46, 0xa9, 0xde, 0xac, 0xd8,
        0xa8, 0xfa, 0x76, 0x3f, 0xe3, 0x9c, 0x34, 0x3f, 0xf9, 0xdc, 0xbb, 0xc7, 0xc7, 0x0b, 0x4f, 0x1d,
        0x8a, 0x51, 0xe0, 0x4b, 0xcd, 0xb4, 0x59, 0x31, 0xc8, 0x9f, 0x7e, 0xc9, 0xd9, 0x78, 0x73, 0x64,
        0xea, 0xc5, 0xac, 0x83, 0x34, 0xd3, 0xeb, 0xc3, 0xc5, 0x81, 0xa0, 0xff, 0xfa, 0x13, 0x63, 0xeb,
        0x17, 0x0d, 0xdd, 0x51, 0xb7, 0xf0, 0xda, 0x49, 0xd3, 0x16, 0x55, 0x26, 0x29, 0xd4, 0x68, 0x9e,
        0x2b, 0x16, 0xbe, 0x58, 0x7d, 0x47, 0xa1, 0xfc, 0x8f, 0xf8, 0xb8, 0xd1, 0x7a, 0xd0, 0x31, 0xce,
        0x45, 0xcb, 0x3a, 0x8f, 0x95, 0x16, 0x04, 0x28, 0xaf, 0xd7, 0xfb, 0xca, 0xbb, 0x4b, 0x40, 0x7e,
    ];

    // -------------------------------------------------------------------------
    // Storage — single ulong, no unsafe/fixed needed
    // -------------------------------------------------------------------------
    private readonly ulong _value;

    // -------------------------------------------------------------------------
    // Constructors
    // -------------------------------------------------------------------------
    private XxHash3(ulong value) {
        this._value = value;
    }

    internal XxHash3(ReadOnlySpan<byte> source) {
        if(source.Length != HashSizeInBytes) {
            throw new ArgumentException(
                $"Source span must be exactly {HashSizeInBytes} bytes long.", nameof(source));
        }
        this._value = MemoryMarshal.Read<ulong>(source);
    }

    // -------------------------------------------------------------------------
    // Factory methods
    // -------------------------------------------------------------------------

    /// <summary>An <see cref="XxHash3"/> consisting of all zero bytes.</summary>
    public static readonly XxHash3 Empty = default;

    /// <summary>Creates an <see cref="XxHash3"/> from an 8-byte span.</summary>
    public static XxHash3 FromBytes(ReadOnlySpan<byte> source) {
        return new(source);
    }

    /// <summary>Creates an <see cref="XxHash3"/> from a hexadecimal string.</summary>
    public static XxHash3 From(HexString hex) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException(
                $"Source HexString must represent exactly {HashSizeInBytes} bytes (16 hex characters).");
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        return new(buffer);
    }

    /// <summary>Creates an <see cref="XxHash3"/> from a Base64 string.</summary>
    public static XxHash3 From(Base64String base64) {
        if(base64.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException(
                $"Source Base64String must represent exactly {HashSizeInBytes} bytes.");
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(!base64.TryDecode(buffer, out int written) || written != HashSizeInBytes) {
            throw new FormatException("Failed to decode Base64 into Hash.");
        }
        return new(buffer);
    }

    /// <summary>Creates an <see cref="XxHash3"/> from a Base32 string.</summary>
    public static XxHash3 From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base32.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new(buffer);
        }
        throw new FormatException(
            $"Base32 string length mismatch for {HashSizeInBytes}-byte hash.");
    }

    /// <summary>Creates an <see cref="XxHash3"/> from a Base62 string.</summary>
    public static XxHash3 From(Base62String base62) {
        byte[] bytes = base62.ToBytes();
        if(bytes.Length > HashSizeInBytes) {
            for(int i = 0; i < bytes.Length - HashSizeInBytes; i++) {
                if(bytes[i] != 0) {
                    throw new FormatException(
                        "Base62 string represents a value too large for this hash.");
                }
            }
            return new(bytes.AsSpan(bytes.Length - HashSizeInBytes));
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        buffer.Clear();
        bytes.CopyTo(buffer[(HashSizeInBytes - bytes.Length)..]);
        return new(buffer);
    }

    /// <summary>Parses a hexadecimal string into an <see cref="XxHash3"/>.</summary>
    public static XxHash3 Parse(string s) {
        Preca.ThrowIfNull(s);
        if(!TryParse(s, out XxHash3 result)) {
            throw new FormatException(
                $"Input string must represent exactly {HashSizeInBytes} bytes (16 hex characters).");
        }
        return result;
    }

    /// <summary>Parses a span of characters into an <see cref="XxHash3"/>. (Zero-allocation)</summary>
    public static XxHash3 Parse(ReadOnlySpan<char> s) {
        if(!TryParse(s, out XxHash3 result)) {
            throw new FormatException(
                $"Input span must represent exactly {HashSizeInBytes} bytes (16 hex characters).");
        }
        return result;
    }

    /// <summary>Tries to parse a hexadecimal string into an <see cref="XxHash3"/>.</summary>
    public static bool TryParse(string? s, out XxHash3 result) {
        if(HexString.TryParse(s, out HexString hex)) return TryParse(hex, out result);
        result = default;
        return false;
    }

    /// <summary>Tries to parse a span of characters into an <see cref="XxHash3"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> s, out XxHash3 result) {
        if(HexString.TryParse(s, out HexString hex)) return TryParse(hex, out result);
        result = default;
        return false;
    }

    /// <summary>Tries to create an <see cref="XxHash3"/> from a hexadecimal string.</summary>
    public static bool TryParse(HexString hex, out XxHash3 result) {
        if(hex.GetDecodedLength() != HashSizeInBytes) { result = default; return false; }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        result = new(buffer);
        return true;
    }

    // -------------------------------------------------------------------------
    // High-performance computation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Computes the XXHash3 for the contents of a <see cref="Secret{Byte}"/>.
    /// </summary>
    public static XxHash3 Compute(Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        return secret.Expose(span => Compute(span));
    }

    /// <summary>
    /// Computes the XXHash3 of a span of bytes. This method is allocation-free.
    /// </summary>
    [SkipLocalsInit]
    public static XxHash3 Compute(ReadOnlySpan<byte> data) {
        return new(Hash64(data));
    }

    /// <summary>
    /// Computes the XXHash3 for the contents of a <see cref="Secret{Char}"/> using the specified encoding.
    /// </summary>
    public static XxHash3 Compute(Secret<char> secret, Encoding encoding) {
        Preca.ThrowIfNull(secret);
        Preca.ThrowIfNull(encoding);
        return secret.Expose(chars => {
            int maxByteCount = encoding.GetMaxByteCount(chars.Length);
            using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
            int bytesWritten = encoding.GetBytes(chars, buffer.Span);
            return Compute(buffer.Span[..bytesWritten]);
        });
    }

    /// <summary>
    /// Computes the XXHash3 for the contents of a <see cref="Secret{Char}"/> using UTF-8.
    /// </summary>
    public static XxHash3 Compute(Secret<char> secret) {
        return Compute(secret, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the XXHash3 of a string using the specified encoding.
    /// </summary>
    [SkipLocalsInit]
    public static XxHash3 Compute(string text, Encoding encoding) {
        Preca.ThrowIfNull(text);
        Preca.ThrowIfNull(encoding);
        int maxByteCount = encoding.GetMaxByteCount(text.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
        int bytesWritten = encoding.GetBytes(text, buffer.Span);
        return Compute(buffer.Span[..bytesWritten]);
    }

    /// <summary>
    /// Computes the XXHash3 of a string using UTF-8 encoding.
    /// </summary>
    public static XxHash3 Compute(string text) {
        return Compute(text, Encoding.UTF8);
    }

    // -------------------------------------------------------------------------
    // Data access & conversion
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{Byte}"/> view of the 8 hash bytes.
    /// Uses <see cref="MemoryMarshal"/> — no <see langword="unsafe"/> code required.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<byte> AsSpan() {
        return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this._value), 1));
    }

    /// <summary>Provides safe, scoped access to the hash bytes.</summary>
    public readonly void Expose(Action<ReadOnlySpan<byte>> action) {
        action(AsSpan());
    }

    /// <summary>Provides safe, scoped access to the hash bytes and returns a result.</summary>
    public readonly TResult Expose<TResult>(Func<ReadOnlySpan<byte>, TResult> func) {
        return func(AsSpan());
    }

    /// <summary>Copies the hash bytes to a destination span.</summary>
    public void CopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) {
            throw new ArgumentException(
                $"Destination span must be at least {HashSizeInBytes} bytes long.", nameof(destination));
        }
        AsSpan().CopyTo(destination);
    }

    /// <summary>Attempts to copy the hash bytes to the specified destination span.</summary>
    public bool TryCopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) return false;
        AsSpan().CopyTo(destination);
        return true;
    }

    /// <summary>Gets the raw 64-bit hash value.</summary>
    public ulong Value => this._value;

    /// <summary>Encodes the hash bytes into a type-safe <see cref="HexString"/>.</summary>
    public HexString ToHexString() {
        return HexString.FromBytes(AsSpan());
    }

    /// <summary>Encodes the hash bytes into a type-safe <see cref="Base64String"/>.</summary>
    public Base64String ToBase64String() {
        return Base64String.FromBytes(AsSpan());
    }

    /// <summary>Encodes the hash bytes into a type-safe <see cref="Base32String"/>.</summary>
    public Base32String ToBase32String() {
        return Base32String.FromBytes(AsSpan());
    }

    /// <summary>Encodes the hash bytes into a type-safe <see cref="Base62String"/>.</summary>
    public Base62String ToBase62String() {
        return Base62String.FromBytes(AsSpan());
    }

    /// <summary>Returns the hexadecimal string representation of the hash.</summary>
    public override string ToString() {
        return Convert.ToHexString(AsSpan());
    }

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return format is "x" ? Convert.ToHexStringLower(AsSpan()) : Convert.ToHexString(AsSpan());
    }

    readonly bool ISpanFormattable.TryFormat(
        Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(destination.Length < required) { charsWritten = 0; return false; }
        return format.Equals("x", StringComparison.Ordinal)
            ? Convert.TryToHexStringLower(AsSpan(), destination, out charsWritten)
            : Convert.TryToHexString(AsSpan(), destination, out charsWritten);
    }

    bool IUtf8SpanFormattable.TryFormat(
        Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(utf8Destination.Length < required) { bytesWritten = 0; return false; }
        Span<char> charBuf = stackalloc char[required];
        bool ok = format.Equals("x", StringComparison.Ordinal)
            ? Convert.TryToHexStringLower(AsSpan(), charBuf, out _)
            : Convert.TryToHexString(AsSpan(), charBuf, out _);
        if(!ok) { bytesWritten = 0; return false; }
        bytesWritten = Encoding.UTF8.GetBytes(charBuf, utf8Destination);
        return true;
    }

    // -------------------------------------------------------------------------
    // Equality — non-cryptographic: fast integer comparison, not constant-time
    // -------------------------------------------------------------------------

    /// <summary>
    /// Compares two <see cref="XxHash3"/> hashes for equality using a fast integer comparison.
    /// Since XXHash3 is not a cryptographic hash, timing-safe comparison is unnecessary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(XxHash3 other) {
        return this._value == other._value;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) {
        return obj is XxHash3 other && Equals(other);
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode() {
        return (int)(this._value ^ (this._value >> 32));
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Equality"/>
    public static bool operator ==(XxHash3 left, XxHash3 right) {
        return left.Equals(right);
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Inequality"/>
    public static bool operator !=(XxHash3 left, XxHash3 right) {
        return !left.Equals(right);
    }

    // =========================================================================
    // XXHash3-64 algorithm (seed = 0, default secret)
    // Reference: https://github.com/Cyan4973/xxHash
    // =========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Read64(ReadOnlySpan<byte> src, int offset) {
        return MemoryMarshal.Read<ulong>(src[offset..]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Read32(ReadOnlySpan<byte> src, int offset) {
        return MemoryMarshal.Read<uint>(src[offset..]);
    }

    /// <summary>128-bit multiply folded into 64 bits.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mul128Fold64(ulong lhs, ulong rhs) {
        UInt128 product = (UInt128)lhs * rhs;
        return (ulong)product ^ (ulong)(product >> 64);
    }

    /// <summary>XXH64 avalanche — used for 1–3 byte inputs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Xxh64Avalanche(ulong h) {
        h ^= h >> 33;
        h *= Prime64_2;
        h ^= h >> 29;
        h *= Prime64_3;
        h ^= h >> 32;
        return h;
    }

    /// <summary>XXH3 avalanche — used for all other cases.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Xxh3Avalanche(ulong h) {
        h ^= h >> 37;
        h *= 0x165667919E3779F9UL;
        h ^= h >> 32;
        return h;
    }

    /// <summary>rrmxmx finalizer — used for 4–8 byte inputs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Rrmxmx(ulong h, int len) {
        h ^= BitOperations.RotateLeft(h, 49) ^ BitOperations.RotateLeft(h, 24);
        h *= 0x9FB21C651E98DF25UL;
        h ^= (h >> 35) + (ulong)len;
        h *= 0x9FB21C651E98DF25UL;
        h ^= h >> 28;
        return h;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix16B(
        ReadOnlySpan<byte> input, int inputOff,
        ReadOnlySpan<byte> secret, int secretOff) {
        return Mul128Fold64(
            Read64(input, inputOff) ^ Read64(secret, secretOff),
            Read64(input, inputOff + 8) ^ Read64(secret, secretOff + 8));
    }

    // ----- Top-level dispatch -----

    private static ulong Hash64(ReadOnlySpan<byte> data) {
        if(data.Length <= 16) return Hash64_0To16(data);
        if(data.Length <= 128) return Hash64_17To128(data);
        if(data.Length <= 240) return Hash64_129To240(data);
        return Hash64_Long(data);
    }

    // ----- 0..16 bytes -----

    private static ulong Hash64_0To16(ReadOnlySpan<byte> data) {
        ReadOnlySpan<byte> secret = DefaultSecret;
        int len = data.Length;
        if(len > 8) return Hash64_9To16(data, secret);
        if(len >= 4) return Hash64_4To8(data, secret);
        if(len > 0) return Hash64_1To3(data, secret);

        // len == 0
        ulong flip1 = Read64(secret, 56) ^ Read64(secret, 64);
        ulong flip2 = Read64(secret, 72) ^ Read64(secret, 80);
        return Xxh3Avalanche(Prime64_5 ^ flip1 ^ flip2);
    }

    private static ulong Hash64_1To3(ReadOnlySpan<byte> data, ReadOnlySpan<byte> secret) {
        int len = data.Length;
        uint c1 = data[0];
        uint c2 = data[len >> 1];
        uint c3 = data[len - 1];
        uint combined = (c1 << 16) | (c2 << 24) | c3 | ((uint)len << 8);
        ulong bitflip = Read32(secret, 0) ^ Read32(secret, 4);
        return Xxh64Avalanche(combined ^ bitflip);
    }

    private static ulong Hash64_4To8(ReadOnlySpan<byte> data, ReadOnlySpan<byte> secret) {
        int len = data.Length;
        ulong input1 = Read32(data, 0);
        ulong input2 = Read32(data, len - 4);
        ulong bitflip = Read64(secret, 8) ^ Read64(secret, 16);
        ulong input64 = input2 + (input1 << 32);
        return Rrmxmx(input64 ^ bitflip, len);
    }

    private static ulong Hash64_9To16(ReadOnlySpan<byte> data, ReadOnlySpan<byte> secret) {
        int len = data.Length;
        ulong bitflip1 = Read64(secret, 24) ^ Read64(secret, 32);
        ulong bitflip2 = Read64(secret, 40) ^ Read64(secret, 48);
        ulong inputLo = Read64(data, 0) ^ bitflip1;
        ulong inputHi = Read64(data, len - 8) ^ bitflip2;
        ulong acc = (ulong)len
                  + BinaryPrimitives.ReverseEndianness(inputLo)
                  + inputHi
                  + Mul128Fold64(inputLo, inputHi);
        return Xxh3Avalanche(acc);
    }

    // ----- 17..128 bytes -----

    private static ulong Hash64_17To128(ReadOnlySpan<byte> data) {
        ReadOnlySpan<byte> secret = DefaultSecret;
        int len = data.Length;
        ulong acc = (ulong)len * Prime64_1;

        if(len > 32) {
            if(len > 64) {
                if(len > 96) {
                    acc += Mix16B(data, 48, secret, 96);
                    acc += Mix16B(data, len - 64, secret, 112);
                }
                acc += Mix16B(data, 32, secret, 64);
                acc += Mix16B(data, len - 48, secret, 80);
            }
            acc += Mix16B(data, 16, secret, 32);
            acc += Mix16B(data, len - 32, secret, 48);
        }
        acc += Mix16B(data, 0, secret, 0);
        acc += Mix16B(data, len - 16, secret, 16);
        return Xxh3Avalanche(acc);
    }

    // ----- 129..240 bytes -----

    private static ulong Hash64_129To240(ReadOnlySpan<byte> data) {
        ReadOnlySpan<byte> secret = DefaultSecret;
        int len = data.Length;
        ulong acc = (ulong)len * Prime64_1;
        int nbRounds = len / 16;

        for(int i = 0; i < 8; i++) {
            acc += Mix16B(data, 16 * i, secret, 16 * i);
        }
        acc = Xxh3Avalanche(acc);

        for(int i = 8; i < nbRounds; i++) {
            acc += Mix16B(data, 16 * i, secret, 16 * (i - 8) + MidSizeStartOffset);
        }
        // Last 16 bytes: secret offset = 136 - 17 = 119
        acc += Mix16B(data, len - 16, secret, 136 - MidSizeLastOffset);
        return Xxh3Avalanche(acc);
    }

    // ----- 241+ bytes (long hash) -----

    [SkipLocalsInit]
    private static ulong Hash64_Long(ReadOnlySpan<byte> data) {
        ReadOnlySpan<byte> secret = DefaultSecret;

        Span<ulong> acc =
        [
            Prime32_3,
            Prime64_1,
            Prime64_2,
            Prime64_3,
            Prime64_4,
            Prime32_2,
            Prime64_5,
            Prime32_1,
        ];
        int len = data.Length;
        int nbBlocks = (len - 1) / BlockLen;

        // Full blocks
        for(int n = 0; n < nbBlocks; n++) {
            AccumulateBlock(acc, data[(n * BlockLen)..], secret);
            ScrambleAcc(acc, secret[^StripLen..]);
        }

        // Last partial block (no scramble after)
        int nbStripes = ((len - 1) - BlockLen * nbBlocks) / StripLen;
        AccumulateStripes(acc, data[(nbBlocks * BlockLen)..], secret, nbStripes);

        // Last stripe — may overlap with previous; uses last 7 bytes of secret as offset
        AccumulateStripe(acc, data[(len - StripLen)..],
            secret[(secret.Length - StripLen - 7)..]);

        return MergeAccs(acc, secret, SecretMergeAccsStart, (ulong)len * Prime64_1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateBlock(Span<ulong> acc, ReadOnlySpan<byte> data, ReadOnlySpan<byte> secret) {
        for(int n = 0; n < NbStripesPerBlock; n++) {
            AccumulateStripe(acc, data[(n * StripLen)..], secret[(n * 8)..]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateStripes(
        Span<ulong> acc, ReadOnlySpan<byte> data, ReadOnlySpan<byte> secret, int nbStripes) {
        for(int n = 0; n < nbStripes; n++) {
            AccumulateStripe(acc, data[(n * StripLen)..], secret[(n * 8)..]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateStripe(
        Span<ulong> acc, ReadOnlySpan<byte> data, ReadOnlySpan<byte> secret) {
        for(int i = 0; i < NbAccs; i++) {
            ulong dataVal = Read64(data, i * 8);
            ulong dataKey = dataVal ^ Read64(secret, i * 8);
            acc[i ^ 1] += dataVal;
            acc[i] += (uint)dataKey * (ulong)(uint)(dataKey >> 32);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ScrambleAcc(Span<ulong> acc, ReadOnlySpan<byte> secret) {
        for(int i = 0; i < NbAccs; i++) {
            ulong acc64 = acc[i];
            acc64 ^= acc64 >> 47;
            acc64 ^= Read64(secret, i * 8);
            acc64 *= Prime32_1;
            acc[i] = acc64;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MergeAccs(
        ReadOnlySpan<ulong> acc, ReadOnlySpan<byte> secret, int secretOff, ulong start) {
        ulong result = start;
        result += Mul128Fold64(acc[0] ^ Read64(secret, secretOff), acc[1] ^ Read64(secret, secretOff + 8));
        result += Mul128Fold64(acc[2] ^ Read64(secret, secretOff + 16), acc[3] ^ Read64(secret, secretOff + 24));
        result += Mul128Fold64(acc[4] ^ Read64(secret, secretOff + 32), acc[5] ^ Read64(secret, secretOff + 40));
        result += Mul128Fold64(acc[6] ^ Read64(secret, secretOff + 48), acc[7] ^ Read64(secret, secretOff + 56));
        return Xxh3Avalanche(result);
    }
}

/// <summary>Extension methods for <see cref="XxHash3"/>.</summary>
public static partial class XxHash3Extensions {
    extension(XxHash3) {
        /// <summary>
        /// Asynchronously computes the <see cref="XxHash3"/> of a stream without loading
        /// the entire content into memory in one allocation. Chunks are buffered via
        /// <see cref="ArrayPool{T}"/> and accumulated in a <see cref="MemoryStream"/>.
        /// </summary>
        public static async ValueTask<XxHash3> ComputeAsync(
            Stream stream, CancellationToken cancellationToken = default) {
            Preca.ThrowIfNull(stream);
            if(stream.CanSeek) stream.Position = 0;

            byte[] rented = ArrayPool<byte>.Shared.Rent(81_920); // 80 KB chunks
            MemoryStream ms = new();

            try {
                int bytesRead;
                while((bytesRead = await stream.ReadAsync(rented, cancellationToken)
                    .ConfigureAwait(false)) > 0) {
                    ms.Write(rented, 0, bytesRead);
                }

                ReadOnlySpan<byte> data = ms.TryGetBuffer(out ArraySegment<byte> seg)
                    ? seg.AsSpan()
                    : ms.ToArray().AsSpan();

                return XxHash3.Compute(data);
            }
            finally {
                ArrayPool<byte>.Shared.Return(rented);
                if(stream.CanSeek) stream.Position = 0;
            }
        }
    }
}