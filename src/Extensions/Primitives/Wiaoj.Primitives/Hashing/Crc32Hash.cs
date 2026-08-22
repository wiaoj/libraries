using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives.Hashing;

/// <summary>
/// Represents an immutable, fixed-size 4-byte (32-bit) IEEE 802.3 CRC32 checksum.
/// </summary>
/// <remarks>
/// <para>
/// <b>Non-Cryptographic:</b> CRC32 is designed strictly for data integrity verification and error detection (e.g., Ethernet, ZIP, PNG), not for cryptographic security.
/// </para>
/// <para>
/// <b>Hardware Acceleration:</b> Computation automatically leverages hardware intrinsics (<see cref="Sse42"/> on x86/x64 or <see cref="System.Runtime.Intrinsics.Arm.Crc32"/> on ARM) with a fallback to a precomputed lookup table.
/// </para>
/// <para>
/// <b>Zero Heap Allocation:</b> The 4-byte digest is stored inline within the struct, ensuring zero allocations in performance-critical paths.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(Crc32HashJsonConverter))]
public unsafe struct Crc32Hash
    : IEquatable<Crc32Hash>,
    IComparable<Crc32Hash>,
    IComparable,
    IParsable<Crc32Hash>,
    ISpanParsable<Crc32Hash>,
    IUtf8SpanParsable<Crc32Hash>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<Crc32Hash, Crc32Hash, bool>,
    IComparisonOperators<Crc32Hash, Crc32Hash, bool> {

    /// <summary>
    /// The initial state value required for the standard IEEE 802.3 CRC32 algorithm (0xFFFFFFFF).
    /// </summary>
    internal const uint Crc32InitialState = 0xFFFFFFFF;

    /// <summary>
    /// The standard reversed polynomial value (0xEDB88320) for IEEE 802.3 CRC32.
    /// </summary>
    private const uint Crc32Polynomial = 0xEDB88320;

    /// <summary>The size of the CRC32 checksum in bytes (4 bytes / 32 bits).</summary>
    internal const int HashSizeInBytes = 4;

    private fixed byte _bytes[HashSizeInBytes];

    /// <summary>Precomputed 256-entry lookup table for software fallback computation.</summary>
    private static readonly uint[] Crc32Table = GenerateTable();

    private static uint[] GenerateTable() {
        uint[] table = new uint[256];
        for(uint i = 0; i < 256; i++) {
            uint res = i;
            for(int j = 0; j < 8; j++) {
                if((res & 1) == 1) res = (res >> 1) ^ Crc32Polynomial;
                else res >>= 1;
            }
            table[i] = res;
        }
        return table;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Crc32Hash"/> struct from a 4-byte span.
    /// </summary>
    /// <param name="source">A span containing exactly 4 bytes of checksum data.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is not exactly 4 bytes long.</exception>
    internal Crc32Hash(ReadOnlySpan<byte> source) {
        Preca.ThrowIf(
            source.Length != HashSizeInBytes,
            () => new ArgumentException($"Source span must be exactly {HashSizeInBytes} bytes long.", nameof(source)));

        fixed(byte* p = this._bytes) {
            source.CopyTo(new Span<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Crc32Hash"/> struct from a 32-bit unsigned integer in Little-Endian format.
    /// </summary>
    /// <param name="value">The 32-bit unsigned integer checksum value.</param>
    public Crc32Hash(uint value) {
        fixed(byte* p = this._bytes) {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(p, HashSizeInBytes), value);
        }
    }

    #region Factory Methods

    /// <summary>
    /// Represents an empty (zero-filled) 4-byte CRC32 checksum.
    /// </summary>
    public static readonly Crc32Hash Empty = default;

    /// <summary>
    /// Gets the checksum value as a 32-bit unsigned integer (Little Endian).
    /// </summary>
    public uint Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(AsSpan());
    }

    /// <summary>
    /// Creates a <see cref="Crc32Hash"/> instance from a 4-byte read-only span.
    /// </summary>
    /// <param name="source">A span containing exactly 4 bytes of checksum data.</param>
    /// <returns>A valid <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is not exactly 4 bytes long.</exception>
    public static Crc32Hash FromBytes(ReadOnlySpan<byte> source) {
        return new Crc32Hash(source);
    }

    /// <summary>
    /// Creates a <see cref="Crc32Hash"/> instance from a hexadecimal string representation.
    /// </summary>
    /// <param name="hex">The hex-encoded string representing the 4-byte checksum (8 hex characters).</param>
    /// <returns>A new <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="hex"/> does not decode to exactly 4 bytes.</exception>
    public static Crc32Hash From(HexString hex) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException($"Source HexString must represent exactly {HashSizeInBytes} bytes (8 hex characters).");
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        return new Crc32Hash(buffer);
    }

    /// <summary>
    /// Creates a <see cref="Crc32Hash"/> instance from a Base64-encoded string.
    /// </summary>
    /// <param name="base64">The Base64-encoded string representing the 4-byte checksum.</param>
    /// <returns>A new <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base64"/> does not decode to exactly 4 bytes.</exception>
    public static Crc32Hash From(Base64String base64) {
        if(base64.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException($"Source Base64String must represent exactly {HashSizeInBytes} bytes.");
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(!base64.TryDecode(buffer, out int written) || written != HashSizeInBytes) {
            throw new FormatException("Failed to decode Base64 into Checksum.");
        }
        return new Crc32Hash(buffer);
    }

    /// <summary>
    /// Creates a <see cref="Crc32Hash"/> instance from a Base32-encoded string.
    /// </summary>
    /// <param name="base32">The Base32-encoded string representing the 4-byte checksum.</param>
    /// <returns>A new <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base32"/> does not decode to exactly 4 bytes.</exception>
    public static Crc32Hash From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base32.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new(buffer);
        }
        throw new FormatException($"Base32 string length mismatch for {HashSizeInBytes}-byte hash.");
    }

    /// <summary>
    /// Creates a <see cref="Crc32Hash"/> instance from a Base62-encoded string.
    /// </summary>
    /// <param name="base62">The Base62-encoded string representing the 4-byte checksum.</param>
    /// <returns>A new <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base62"/> represents a value exceeding 4 bytes.</exception>
    public static Crc32Hash From(Base62String base62) {
        byte[] bytes = base62.ToBytes();

        if(bytes.Length > HashSizeInBytes) {
            for(int i = 0; i < bytes.Length - HashSizeInBytes; i++) {
                if(bytes[i] != 0) throw new FormatException("Base62 string represents a value too large for this hash.");
            }
            return new(bytes.AsSpan(bytes.Length - HashSizeInBytes));
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        buffer.Clear();
        bytes.CopyTo(buffer[(HashSizeInBytes - bytes.Length)..]);
        return new(buffer);
    }

    /// <summary>
    /// Parses an 8-character hexadecimal string into a <see cref="Crc32Hash"/>.
    /// </summary>
    /// <param name="s">The hexadecimal string to parse.</param>
    /// <returns>The parsed <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="s"/> is not a valid 8-character hexadecimal string.</exception>
    public static Crc32Hash Parse(string s) {
        Preca.ThrowIfNull(s);
        if(!TryParse(s.AsSpan(), out Crc32Hash result)) {
            throw new FormatException($"Input string must represent exactly {HashSizeInBytes} bytes (8 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses an 8-character hexadecimal span into a <see cref="Crc32Hash"/> without heap allocations.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <returns>The parsed <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="s"/> is not a valid 8-character hexadecimal sequence.</exception>
    public static Crc32Hash Parse(ReadOnlySpan<char> s) {
        if(!TryParse(s, out Crc32Hash result)) {
            throw new FormatException($"Input span must represent exactly {HashSizeInBytes} bytes (8 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a UTF-8 encoded hexadecimal byte span into a <see cref="Crc32Hash"/>.
    /// </summary>
    public static Crc32Hash Parse(ReadOnlySpan<byte> utf8Text) {
        if(!TryParse(utf8Text, out Crc32Hash result)) {
            throw new FormatException("Invalid UTF-8 hexadecimal sequence for Crc32Hash.");
        }
        return result;
    }

    /// <summary>
    /// Attempts to parse a hexadecimal string into a <see cref="Crc32Hash"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out Crc32Hash result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a hexadecimal span into a <see cref="Crc32Hash"/> without heap allocations.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out Crc32Hash result) {
        if(HexString.TryParse(s, out HexString hex)) {
            return TryParse(hex, out result);
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a UTF-8 encoded byte span into a <see cref="Crc32Hash"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Crc32Hash result) {
        if(utf8Text.Length == HashSizeInBytes * 2) {
            Span<char> chars = stackalloc char[HashSizeInBytes * 2];
            if(Encoding.UTF8.GetChars(utf8Text, chars) == HashSizeInBytes * 2) {
                return TryParse(chars, out result);
            }
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a <see cref="HexString"/> into a <see cref="Crc32Hash"/>.
    /// </summary>
    /// <param name="hex">The hex-encoded string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed hash if successful; otherwise, default.</param>
    /// <returns><see langword="true"/> if <paramref name="hex"/> represents exactly 4 bytes; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(HexString hex, out Crc32Hash result) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            result = default;
            return false;
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        result = new Crc32Hash(buffer);
        return true;
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static Crc32Hash IParsable<Crc32Hash>.Parse(string s, IFormatProvider? provider) => Parse(s);
    static bool IParsable<Crc32Hash>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Crc32Hash result) => TryParse(s, out result);
    static Crc32Hash ISpanParsable<Crc32Hash>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s);
    static bool ISpanParsable<Crc32Hash>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Crc32Hash result) => TryParse(s, out result);
    static Crc32Hash IUtf8SpanParsable<Crc32Hash>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) => Parse(utf8Text);
    static bool IUtf8SpanParsable<Crc32Hash>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Crc32Hash result) => TryParse(utf8Text, out result);

    #endregion

    #region High-Performance Computation

    /// <summary>
    /// Appends data to an ongoing CRC32 calculation state using hardware intrinsics when supported.
    /// </summary>
    /// <param name="crc">The current CRC32 state.</param>
    /// <param name="data">The byte span to append.</param>
    /// <returns>The updated CRC32 state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Append(uint crc, ReadOnlySpan<byte> data) {
        // x86/x64 — Hardware accelerated via SSE 4.2
        if(Sse42.IsSupported) {
            int i = 0;
            // 4-byte chunks
            for(; i <= data.Length - 4; i += 4)
                crc = Sse42.Crc32(crc, MemoryMarshal.Read<uint>(data[i..]));
            // Remaining tail bytes
            for(; i < data.Length; i++)
                crc = Sse42.Crc32(crc, data[i]);
            return crc;
        }

        // ARM — Hardware accelerated via ARM CRC32 extension
        if(Crc32.IsSupported) {
            int i = 0;
            for(; i <= data.Length - 4; i += 4)
                crc = Crc32.ComputeCrc32(crc, MemoryMarshal.Read<uint>(data[i..]));
            for(; i < data.Length; i++)
                crc = Crc32.ComputeCrc32(crc, data[i]);
            return crc;
        }

        // Software fallback using precomputed lookup table
        uint[] table = Crc32Table;
        for(int i = 0; i < data.Length; i++)
            crc = (crc >> 8) ^ table[(crc ^ data[i]) & 0xFF];
        return crc;
    }

    /// <summary>
    /// Computes the CRC32 checksum for the contents of a secure <see cref="Secret{Byte}"/>.
    /// </summary>
    /// <param name="secret">The secret byte data to checksum.</param>
    /// <returns>A new <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is null.</exception>
    public static Crc32Hash Compute(Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        return secret.Expose(span => Compute(span));
    }

    /// <summary>
    /// Computes the CRC32 checksum of a byte span without heap allocations.
    /// </summary>
    /// <param name="data">The byte span to checksum.</param>
    /// <returns>A new <see cref="Crc32Hash"/> instance.</returns>
    [SkipLocalsInit]
    public static Crc32Hash Compute(ReadOnlySpan<byte> data) {
        uint crc = Append(Crc32InitialState, data);
        return new Crc32Hash(~crc);
    }

    /// <summary>
    /// Computes the CRC32 checksum for the contents of a secure <see cref="Secret{Char}"/> using the specified encoding.
    /// </summary>
    /// <param name="secret">The secret character data to checksum.</param>
    /// <param name="encoding">The character encoding used to convert characters to bytes.</param>
    /// <returns>A new <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> or <paramref name="encoding"/> is null.</exception>
    public static Crc32Hash Compute(Secret<char> secret, Encoding encoding) {
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
    /// Computes the CRC32 checksum for the contents of a secure <see cref="Secret{Char}"/> using UTF-8 encoding.
    /// </summary>
    /// <param name="secret">The secret character data to checksum.</param>
    /// <returns>A new <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secret"/> is null.</exception>
    public static Crc32Hash Compute(Secret<char> secret) {
        return Compute(secret, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the CRC32 checksum of a string using the specified encoding.
    /// </summary>
    /// <param name="text">The string to checksum.</param>
    /// <param name="encoding">The character encoding to use.</param>
    /// <returns>A new <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="encoding"/> is null.</exception>
    [SkipLocalsInit]
    public static Crc32Hash Compute(string text, Encoding encoding) {
        Preca.ThrowIfNull(text);
        Preca.ThrowIfNull(encoding);

        int maxByteCount = encoding.GetMaxByteCount(text.Length);

        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);

        int bytesWritten = encoding.GetBytes(text, buffer.Span);
        return Compute(buffer.Span[..bytesWritten]);
    }

    /// <summary>
    /// Computes the CRC32 checksum of a string using UTF-8 encoding.
    /// </summary>
    /// <param name="text">The string to checksum.</param>
    /// <returns>A new <see cref="Crc32Hash"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    public static Crc32Hash Compute(string text) {
        return Compute(text, Encoding.UTF8);
    }

    #endregion

    #region Data Access & Conversion

    /// <summary>
    /// Provides safe, scoped access to the checksum bytes as a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    /// <param name="action">The delegate receiving the read-only span.</param>
    public void Expose(Action<ReadOnlySpan<byte>> action) {
        fixed(byte* p = this._bytes) {
            action(new ReadOnlySpan<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Provides safe, scoped access to the checksum bytes and returns a result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the delegate.</typeparam>
    /// <param name="func">The delegate receiving the read-only span and returning a result.</param>
    /// <returns>The result computed by <paramref name="func"/>.</returns>
    public TResult Expose<TResult>(Func<ReadOnlySpan<byte>, TResult> func) {
        fixed(byte* p = this._bytes) {
            return func(new ReadOnlySpan<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Copies the checksum bytes into a destination span.
    /// </summary>
    /// <param name="destination">The destination span. Must be at least 4 bytes long.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than 4 bytes.</exception>
    public void CopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) {
            throw new ArgumentException($"Destination span must be at least {HashSizeInBytes} bytes long.", nameof(destination));
        }
        AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Attempts to copy the checksum bytes into the specified destination span.
    /// </summary>
    /// <param name="destination">The span to copy the bytes into.</param>
    /// <returns><see langword="true"/> if the copy was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryCopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) return false;
        AsSpan().CopyTo(destination);
        return true;
    }

    /// <summary>
    /// Returns a direct <see cref="ReadOnlySpan{Byte}"/> view over the inline checksum bytes.
    /// </summary>
    /// <returns>A 4-byte <see cref="ReadOnlySpan{Byte}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> AsSpan() {
        return new ReadOnlySpan<byte>(Unsafe.AsPointer(ref Unsafe.AsRef(in this._bytes[0])), HashSizeInBytes);
    }

    /// <summary>
    /// Encodes the checksum bytes into an uppercase <see cref="HexString"/>.
    /// </summary>
    /// <returns>An uppercase <see cref="HexString"/> representation of the checksum.</returns>
    public HexString ToHexString() {
        return HexString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the checksum bytes into a lowercase <see cref="HexString"/> without allocations.
    /// </summary>
    /// <returns>A lowercase <see cref="HexString"/> representation of the checksum.</returns>
    public HexString ToHexStringLower() {
        return HexString.FromBytesLower(AsSpan());
    }

    /// <summary>
    /// Encodes the checksum bytes into a type-safe <see cref="Base64String"/>.
    /// </summary>
    /// <returns>A <see cref="Base64String"/> representation of the checksum.</returns>
    public Base64String ToBase64String() {
        return Base64String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the checksum bytes into a type-safe, URL-safe <see cref="Base64UrlString"/>.
    /// </summary>
    /// <returns>A <see cref="Base64UrlString"/> representation of the checksum.</returns>
    public Base64UrlString ToBase64UrlString() {
        return Base64UrlString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the checksum bytes into a type-safe <see cref="Base32String"/>.
    /// </summary>
    /// <returns>A <see cref="Base32String"/> representation of the checksum.</returns>
    public Base32String ToBase32String() {
        return Base32String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the checksum bytes into a type-safe <see cref="Base62String"/>.
    /// </summary>
    /// <returns>A <see cref="Base62String"/> representation of the checksum.</returns>
    public Base62String ToBase62String() {
        return Base62String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Returns the uppercase hexadecimal string representation of the checksum.
    /// </summary>
    /// <returns>An uppercase 8-character hexadecimal string.</returns>
    public override readonly string ToString() {
        return string.Create(HashSizeInBytes * 2, this, (span, hash) => {
            hash.TryFormat(span, out _);
        });
    }

    /// <summary>
    /// Returns the string representation of the checksum formatted using the specified format.
    /// </summary>
    /// <param name="format">The format specifier.</param>
    /// <returns>A string representation of the checksum.</returns>
    public string ToString(string? format) => ToString(format, null);

    /// <summary>
    /// Returns the string representation of the checksum formatted using the specified format and provider.
    /// </summary>
    /// <param name="format">The format specifier.</param>
    /// <param name="formatProvider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A string representation of the checksum.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider) {
        return format is "x" ? Convert.ToHexStringLower(AsSpan()) : Convert.ToHexString(AsSpan());
    }

    /// <summary>
    /// Attempts to format the checksum as an uppercase hexadecimal string into the destination character span.
    /// </summary>
    /// <param name="destination">The span of characters to write to.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, null);

    /// <summary>
    /// Attempts to format the checksum as a hexadecimal string into the destination character span.
    /// </summary>
    /// <param name="destination">The span of characters to write to.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <param name="lowerCase"><see langword="true"/> to format as lowercase; otherwise, <see langword="false"/> for uppercase.</param>
    /// <returns><see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten, bool lowerCase) {
        int required = HashSizeInBytes * 2;
        if(destination.Length < required) {
            charsWritten = 0;
            return false;
        }

        return lowerCase
            ? Convert.TryToHexStringLower(AsSpan(), destination, out charsWritten)
            : Convert.TryToHexString(AsSpan(), destination, out charsWritten);
    }

    /// <summary>
    /// Attempts to format the checksum into the destination character span using the specified format.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) => TryFormat(destination, out charsWritten, format, null);

    /// <summary>
    /// Attempts to format the checksum into the destination character span using the specified format and provider.
    /// </summary>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(destination.Length < required) { charsWritten = 0; return false; }
        bool lower = format.Equals("x", StringComparison.Ordinal);
        return lower
            ? Convert.TryToHexStringLower(AsSpan(), destination, out charsWritten)
            : Convert.TryToHexString(AsSpan(), destination, out charsWritten);
    }

    /// <summary>
    /// Attempts to format the checksum as an uppercase UTF-8 hexadecimal string into the destination byte span.
    /// </summary>
    /// <param name="destination">The span of bytes to write to.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> destination, out int bytesWritten) => TryFormat(destination, out bytesWritten, default, null);

    /// <summary>
    /// Attempts to format the checksum as a UTF-8 hexadecimal string into the destination byte span.
    /// </summary>
    /// <param name="destination">The span of bytes to write to.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <param name="lowerCase"><see langword="true"/> to format as lowercase; otherwise, <see langword="false"/> for uppercase.</param>
    /// <returns><see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> destination, out int bytesWritten, bool lowerCase) {
        int required = HashSizeInBytes * 2;
        if(destination.Length < required) {
            bytesWritten = 0;
            return false;
        }

        return lowerCase
            ? Convert.TryToHexStringLower(AsSpan(), destination, out bytesWritten)
            : Convert.TryToHexString(AsSpan(), destination, out bytesWritten);
    }

    /// <summary>
    /// Attempts to format the checksum into the destination UTF-8 byte span using the specified format.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) => TryFormat(utf8Destination, out bytesWritten, format, null);

    /// <summary>
    /// Attempts to format the checksum into the destination UTF-8 byte span using the specified format and provider.
    /// </summary>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(utf8Destination.Length < required) { bytesWritten = 0; return false; }
        Span<char> charBuf = stackalloc char[required];
        bool lower = format.Equals("x", StringComparison.Ordinal);
        bool ok = lower
            ? Convert.TryToHexStringLower(AsSpan(), charBuf, out _)
            : Convert.TryToHexString(AsSpan(), charBuf, out _);
        if(!ok) { bytesWritten = 0; return false; }
        bytesWritten = Encoding.UTF8.GetBytes(charBuf, utf8Destination);
        return true;
    }

    #endregion

    #region Equality & Comparison

    /// <summary>
    /// Determines whether two <see cref="Crc32Hash"/> instances are equal using an optimized 32-bit integer comparison.
    /// </summary>
    /// <param name="other">The other checksum to compare against.</param>
    /// <returns><see langword="true"/> if both checksums are equal; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Crc32Hash other) {
        return Unsafe.As<byte, uint>(ref Unsafe.AsRef(in this._bytes[0])) ==
               Unsafe.As<byte, uint>(ref Unsafe.AsRef(in other._bytes[0]));
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        return obj is Crc32Hash other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return (int)Unsafe.As<byte, uint>(ref Unsafe.AsRef(in this._bytes[0]));
    }

    /// <inheritdoc/>
    public int CompareTo(Crc32Hash other) => AsSpan().SequenceCompareTo(other.AsSpan());

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is Crc32Hash other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(Crc32Hash)}.", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(Crc32Hash left, Crc32Hash right) => left.CompareTo(right) > 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(Crc32Hash left, Crc32Hash right) => left.CompareTo(right) < 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(Crc32Hash left, Crc32Hash right) => left.CompareTo(right) >= 0;

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(Crc32Hash left, Crc32Hash right) => left.CompareTo(right) <= 0;

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(Crc32Hash left, Crc32Hash right) => left.Equals(right);

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(Crc32Hash left, Crc32Hash right) => !left.Equals(right);

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="Crc32Hash"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Crc32Hash> OrdinalComparer => Crc32HashOrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="Crc32Hash"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<Crc32Hash> OrdinalIgnoreCaseComparer => Crc32HashOrdinalIgnoreCaseComparer.Instance;

    private sealed class Crc32HashOrdinalComparer : IEqualityComparer<Crc32Hash>, IAlternateEqualityComparer<ReadOnlySpan<char>, Crc32Hash> {
        public static Crc32HashOrdinalComparer Instance { get; } = new();

        public bool Equals(Crc32Hash x, Crc32Hash y) => x.Equals(y);

        public int GetHashCode(Crc32Hash obj) => obj.GetHashCode();

        public bool Equals(ReadOnlySpan<char> alternate, Crc32Hash other) {
            if(Crc32Hash.TryParse(alternate, out Crc32Hash parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(Crc32Hash.TryParse(alternate, out Crc32Hash parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        public Crc32Hash Create(ReadOnlySpan<char> alternate) => Crc32Hash.Parse(alternate);
    }

    private sealed class Crc32HashOrdinalIgnoreCaseComparer : IEqualityComparer<Crc32Hash>, IAlternateEqualityComparer<ReadOnlySpan<char>, Crc32Hash> {
        public static Crc32HashOrdinalIgnoreCaseComparer Instance { get; } = new();

        public bool Equals(Crc32Hash x, Crc32Hash y) => x.Equals(y);

        public int GetHashCode(Crc32Hash obj) => obj.GetHashCode();

        public bool Equals(ReadOnlySpan<char> alternate, Crc32Hash other) {
            if(Crc32Hash.TryParse(alternate, out Crc32Hash parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(Crc32Hash.TryParse(alternate, out Crc32Hash parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        public Crc32Hash Create(ReadOnlySpan<char> alternate) => Crc32Hash.Parse(alternate);
    }

    #endregion
}

/// <summary>
/// Extension methods for <see cref="Crc32Hash"/>.
/// </summary>
public static partial class Crc32HashExtensions {
    extension(Crc32Hash) {
        /// <summary>
        /// Asynchronously computes the <see cref="Crc32Hash"/> checksum of a stream without loading the full content into memory.
        /// Resets the stream position before and after computation if seekable.
        /// </summary>
        /// <param name="stream">The source stream to checksum.</param>
        /// <returns>A task containing the computed <see cref="Crc32Hash"/>.</returns>
        public static ValueTask<Crc32Hash> ComputeAsync(Stream stream) => ComputeAsync(stream, CancellationToken.None);

        /// <summary>
        /// Asynchronously computes the <see cref="Crc32Hash"/> checksum of a stream without loading the full content into memory.
        /// Resets the stream position before and after computation if seekable.
        /// </summary>
        /// <param name="stream">The source stream to checksum.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <returns>A task containing the computed <see cref="Crc32Hash"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
        public static async ValueTask<Crc32Hash> ComputeAsync(Stream stream, CancellationToken cancellationToken) {
            Preca.ThrowIfNull(stream);

            if(stream.CanSeek)
                stream.Position = 0;

            uint crc = Crc32Hash.Crc32InitialState;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);

            try {
                int bytesRead;
                while((bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0) {
                    crc = Crc32Hash.Append(crc, buffer.AsSpan(0, bytesRead));
                }
            }
            finally {
                ArrayPool<byte>.Shared.Return(buffer);

                if(stream.CanSeek)
                    stream.Position = 0;
            }

            return new Crc32Hash(~crc);
        }
    }
}