using System.Buffers;
using System.Diagnostics;
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
/// Represents a 4-byte (32-bit)<see cref="Crc32Hash" />checksum. This struct guarantees the correct size
/// and provides high-performance, allocation-free operations for computing and comparing hashes.
/// Non-cryptographic hash used primarily for data integrity verification.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(Crc32HashJsonConverter))]
public unsafe struct Crc32Hash
    : IEquatable<Crc32Hash>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IEqualityOperators<Crc32Hash, Crc32Hash, bool> {

    /// <summary>
    /// Standart IEEE 802.3 CRC32 algoritması için gereken başlangıç state değeri.
    /// </summary>
    internal const uint Crc32InitialState = 0xFFFFFFFF;

    /// <summary>
    /// Standart IEEE 802.3 CRC32 ters çevrilmiş (reversed) polinom değeri.
    /// ZIP, PNG, Ethernet gibi formatlarda kullanılan endüstri standardı polinom.
    /// </summary>
    private const uint Crc32Polynomial = 0xEDB88320;

    internal const int HashSizeInBytes = 4;
    private fixed byte _bytes[HashSizeInBytes];

    /// Precomputed <see cref="Crc32Hash" /> table for high-performance calculations
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

    internal Crc32Hash(ReadOnlySpan<byte> source) {
        Preca.ThrowIf(
            source.Length != HashSizeInBytes,
            () => new ArgumentException("Source span must be exactly 4 bytes long.", nameof(source)));

        fixed(byte* p = this._bytes) {
            source.CopyTo(new Span<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Initializes a new instance from a 32-bit unsigned integer.
    /// </summary>
    public Crc32Hash(uint value) {
        fixed(byte* p = this._bytes) {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(p, HashSizeInBytes), value);
        }
    }

    #region Factory Methods

    /// <summary>
    /// Represents a<see cref="Crc32Hash" />checksum consisting of all zero bytes.
    /// </summary>
    public static readonly Crc32Hash Empty = default;

    /// <summary>
    /// Gets the checksum as a 32-bit unsigned integer (Little Endian).
    /// </summary>
    public uint Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(AsSpan());
    }

    /// <summary>
    /// Creates a <see cref="Crc32Hash" /> instance from a 4-byte span.
    /// </summary>
    public static Crc32Hash FromBytes(ReadOnlySpan<byte> source) {
        return new Crc32Hash(source);
    }

    /// <summary>
    /// Creates a <see cref="Crc32Hash" /> instance from a hexadecimal string representation.
    /// </summary>
    public static Crc32Hash From(HexString hex) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException("Source HexString must represent exactly 4 bytes (8 hex characters).");
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        return new Crc32Hash(buffer);
    }

    /// <summary>
    /// Creates a <see cref="Crc32Hash" /> instance from a Base64String.
    /// </summary>
    public static Crc32Hash From(Base64String base64) {
        if(base64.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException("Source Base64String must represent exactly 4 bytes.");
        }

        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(!base64.TryDecode(buffer, out int written) || written != HashSizeInBytes) {
            throw new FormatException("Failed to decode Base64 into Checksum.");
        }
        return new Crc32Hash(buffer);
    }

    /// <summary>
    /// Creates a hash instance from a valid <see cref="Base32String"/>.
    /// </summary>
    public static Crc32Hash From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base32.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new(buffer);
        }
        throw new FormatException($"Base32 string length mismatch for {HashSizeInBytes}-byte hash.");
    }

    /// <summary>
    /// Creates a hash instance from a valid <see cref="Base62String"/>.
    /// </summary>
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
    /// Parses a hexadecimal string into a Crc32Hash.
    /// </summary>
    public static Crc32Hash Parse(string s) {
        Preca.ThrowIfNull(s);
        if(!TryParse(s, out Crc32Hash result)) {
            throw new FormatException($"Input string must represent exactly {HashSizeInBytes} bytes (8 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a span of characters into a Crc32Hash. (Zero-allocation)
    /// </summary>
    public static Crc32Hash Parse(ReadOnlySpan<char> s) {
        if(!TryParse(s, out Crc32Hash result)) {
            throw new FormatException($"Input span must represent exactly {HashSizeInBytes} bytes (8 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Tries to parse a hexadecimal string into a Crc32Hash.
    /// </summary>
    public static bool TryParse(string? s, out Crc32Hash result) {
        if(HexString.TryParse(s, out HexString hex)) {
            return TryParse(hex, out result);
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Tries to parse a span of characters into a Crc32Hash.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out Crc32Hash result) {
        if(HexString.TryParse(s, out HexString hex)) {
            return TryParse(hex, out result);
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Tries to create a Crc32Hash instance from a hexadecimal string representation.
    /// </summary>
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

    #region High-Performance Computation

    /// <summary>
    /// Internal helper to append data to an existing <see cref="Crc32Hash" /> state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Append(uint crc, ReadOnlySpan<byte> data) {
        // x86/x64 — SSE4.2
        if(Sse42.IsSupported) {
            int i = 0;
            // 4 byte chunk
            for(; i <= data.Length - 4; i += 4)
                crc = Sse42.Crc32(crc, MemoryMarshal.Read<uint>(data[i..]));
            // Kalan byte'lar
            for(; i < data.Length; i++)
                crc = Sse42.Crc32(crc, data[i]);
            return crc;
        }

        // ARM — CRC32 extension
        if(Crc32.IsSupported) {
            int i = 0;
            for(; i <= data.Length - 4; i += 4)
                crc = Crc32.ComputeCrc32(crc, MemoryMarshal.Read<uint>(data[i..]));
            for(; i < data.Length; i++)
                crc = Crc32.ComputeCrc32(crc, data[i]);
            return crc;
        }

        // Fallback lookup
        uint[] table = Crc32Table;
        for(int i = 0; i < data.Length; i++)
            crc = (crc >> 8) ^ table[(crc ^ data[i]) & 0xFF];
        return crc;
    }

    /// <summary>
    /// Computes the <see cref="Crc32Hash" />checksum for the contents of a <see cref="Secret{Byte}"/>.
    /// Note:<see cref="Crc32Hash" />is not a cryptographic hash and should not be used for secure hashing.
    /// </summary>
    public static Crc32Hash Compute(Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        return secret.Expose(span => Compute(span));
    }

    /// <summary>
    /// Computes the <see cref="Crc32Hash" />checksum of a span of bytes. This method is allocation-free.
    /// </summary>
    [SkipLocalsInit]
    public static Crc32Hash Compute(ReadOnlySpan<byte> data) {
        uint crc = Append(Crc32InitialState, data);
        return new Crc32Hash(~crc);
    }

    /// <summary>
    /// Computes the <see cref="Crc32Hash" />checksum for the contents of a <see cref="Secret{T}"/> of <see cref="char"/> using the specified encoding.
    /// </summary>
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
    /// Computes the <see cref="Crc32Hash" />checksum for the contents of a <see cref="Secret{T}"/> of <see cref="char"/> using the default UTF-8 encoding.
    /// </summary>
    public static Crc32Hash Compute(Secret<char> secret) {
        return Compute(secret, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the <see cref="Crc32Hash" />checksum of a string using the specified encoding.
    /// </summary>
    [SkipLocalsInit]
    public static Crc32Hash Compute(string text, Encoding encoding) {
        Preca.ThrowIfNull(text);
        Preca.ThrowIfNull(encoding);

        int maxByteCount = encoding.GetMaxByteCount(text.Length);

        // String to Byte dönüşümünde Heap Allocation (byte[]) oluşmasını engeller
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);

        int bytesWritten = encoding.GetBytes(text, buffer.Span);
        return Compute(buffer.Span[..bytesWritten]);
    }

    /// <summary>
    /// Computes the <see cref="Crc32Hash" />checksum of a string using <see cref="Encoding.UTF8"/> encoding by default.
    /// </summary>
    public static Crc32Hash Compute(string text) {
        return Compute(text, Encoding.UTF8);
    }

    #endregion

    #region Data Access & Conversion

    /// <summary>
    /// Provides safe, scoped access to the hash bytes as a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    public void Expose(Action<ReadOnlySpan<byte>> action) {
        fixed(byte* p = this._bytes) {
            action(new ReadOnlySpan<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Provides safe, scoped access to the hash bytes and returns a result.
    /// </summary>
    public TResult Expose<TResult>(Func<ReadOnlySpan<byte>, TResult> func) {
        fixed(byte* p = this._bytes) {
            return func(new ReadOnlySpan<byte>(p, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Copies the hash bytes to a destination span.
    /// </summary>
    public void CopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) {
            throw new ArgumentException("Destination span must be at least 4 bytes long.", nameof(destination));
        }
        AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Attempts to copy the hash bytes to the specified destination span.
    /// </summary>
    public bool TryCopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) return false;
        AsSpan().CopyTo(destination);
        return true;
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{Byte}"/> view of the hash bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> AsSpan() {
        return new ReadOnlySpan<byte>(Unsafe.AsPointer(ref Unsafe.AsRef(in this._bytes[0])), HashSizeInBytes);
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="HexString"/>.
    /// </summary>
    public HexString ToHexString() {
        return HexString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base64String"/>.
    /// </summary>
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

    /// <summary>
    /// Returns the hexadecimal string representation of the hash.
    /// </summary>
    public override readonly string ToString() {
        return string.Create(HashSizeInBytes * 2, this, (span, hash) => {
            hash.TryFormat(span, out _);
        });
    }

    /// <summary>
    /// Attempts to format the hash as a hexadecimal string into the provided character span.
    /// </summary>
    /// <param name="destination">The span of characters to write to.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        return TryFormat(destination, out charsWritten, false);
    }

    /// <summary>
    /// Attempts to format the hash as a hexadecimal string into the provided character span.
    /// </summary>
    /// <param name="destination">The span of characters to write to.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <param name="lowerCase">If set to <see langword="true"/>, formats as lowercase; otherwise, uppercase.</param>
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
    /// Attempts to format the hash as a UTF-8 hexadecimal string into the provided byte span.
    /// </summary>
    /// <param name="destination">The span of bytes to write to.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param> 
    /// <returns><see langword="true"/> if the formatting was successful; otherwise, <see langword="false"/>.</returns>
    public bool TryFormat(Span<byte> destination, out int bytesWritten) {
        return TryFormat(destination, out bytesWritten, false);
    }

    /// <summary>
    /// Attempts to format the hash as a UTF-8 hexadecimal string into the provided byte span.
    /// </summary>
    /// <param name="destination">The span of bytes to write to.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <param name="lowerCase">If set to <see langword="true"/>, formats as lowercase; otherwise, uppercase.</param>
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

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return format is "x" ? Convert.ToHexStringLower(AsSpan()) : Convert.ToHexString(AsSpan());
    }

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(destination.Length < required) { charsWritten = 0; return false; }
        bool lower = format.Equals("x", StringComparison.Ordinal);
        return lower
            ? Convert.TryToHexStringLower(AsSpan(), destination, out charsWritten)
            : Convert.TryToHexString(AsSpan(), destination, out charsWritten);
    }

    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
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

    #region Equality

    /// <summary>
    /// Compares two<see cref="Crc32Hash" />hashes for equality. 
    /// Note: Since<see cref="Crc32Hash" />is not cryptographic, this uses a highly optimized 32-bit integer comparison.
    /// </summary>
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

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(Crc32Hash left, Crc32Hash right) {
        return left.Equals(right);
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(Crc32Hash left, Crc32Hash right) {
        return !left.Equals(right);
    }

    #endregion
}

/// <summary>
/// Extension methods for <see cref="Crc32Hash"/>.
/// </summary>
public static partial class Crc32HashExtensions {
    extension(Crc32Hash) {
        /// <summary>
        /// Asynchronously computes the <see cref="Crc32Hash" />checksum of a stream without loading it all into memory.
        /// </summary>
        public static async ValueTask<Crc32Hash> ComputeAsync(Stream stream, CancellationToken cancellationToken = default) {
            Preca.ThrowIfNull(stream);

            if(stream.CanSeek)
                stream.Position = 0;

            uint crc = Crc32Hash.Crc32InitialState;
            // Rent a buffer to avoid Large Object Heap (LOH) allocations for streams
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