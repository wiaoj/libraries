using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.Snowflake;

/// <summary>
/// Represents a distributed unique identifier based on the Twitter Snowflake algorithm.
/// This struct is a high-performance, immutable 64-bit integer wrapper optimized for sorting, database efficiency, and zero-allocation parsing.
/// </summary>
[TypeConverter(typeof(SnowflakeIdTypeConverter))]
[JsonConverter(typeof(SnowflakeIdJsonConverter))]
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public readonly struct SnowflakeId :
    IEquatable<SnowflakeId>,
    IComparable<SnowflakeId>,
    IComparable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    ISpanParsable<SnowflakeId>,
    IUtf8SpanParsable<SnowflakeId>,
    IEqualityOperators<SnowflakeId, SnowflakeId, bool>,
    IComparisonOperators<SnowflakeId, SnowflakeId, bool> {

    #region Static Configuration & Generator

    private static SnowflakeGenerator _sharedGenerator = SnowflakeGenerator.Default;
    private static readonly Lock _configLock = new();

    /// <summary>
    /// Configures the global shared generator using a complete options object.
    /// This allows control over SequenceBits, MaxDriftMs, Epoch, etc.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public static void Configure(SnowflakeOptions options) {
        Preca.ThrowIfNull(options);
        _configLock.Enter();
        _sharedGenerator = new SnowflakeGenerator(options);
        _configLock.Exit();
    }

    /// <summary>
    /// Configures the global shared generator with a specific Node ID using the default Epoch.
    /// </summary>
    /// <param name="nodeId">The unique ID of this machine/node.</param>
    public static void Configure(ushort nodeId) {
        Configure(new SnowflakeOptions { NodeId = nodeId });
    }

    /// <summary>
    /// Configures the global shared generator with a specific Node ID and Epoch.
    /// </summary>
    /// <param name="nodeId">The unique ID of this machine/node.</param>
    /// <param name="epoch">The start date for ID generation.</param>
    public static void Configure(ushort nodeId, DateTimeOffset epoch) {
        Configure(new SnowflakeOptions { NodeId = nodeId, Epoch = epoch });
    }

    /// <summary>
    /// Generates a new unique <see cref="SnowflakeId"/> using the shared global configuration.
    /// This method is thread-safe and highly optimized for hot paths.
    /// </summary>
    /// <returns>A new unique SnowflakeId.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static SnowflakeId NewId() {
        return _sharedGenerator.NextId();
    }

    #endregion

    private readonly long _value;

    /// <summary>
    /// Represents an empty or zero-valued SnowflakeId.
    /// </summary>
    public static readonly SnowflakeId Empty = new(0);

    /// <summary>
    /// Initializes a new instance of the <see cref="SnowflakeId"/> struct.
    /// </summary>
    /// <param name="value">The 64-bit integer value.</param>
    public SnowflakeId(long value) {
        this._value = value;
    }

    /// <summary>
    /// Gets the underlying 64-bit integer value of the ID.
    /// </summary>
    public long Value => this._value;

    #region Parsing (Span & UTF-8)

    // --- Public API ---

    /// <summary>
    /// Parses a span of characters into a <see cref="SnowflakeId"/>.
    /// </summary>
    /// <param name="s">The span of characters to parse.</param>
    /// <returns>The parsed SnowflakeId.</returns>
    /// <exception cref="FormatException">Thrown if the input is not in a valid format.</exception>
    public static SnowflakeId Parse(ReadOnlySpan<char> s) {
        if (TryParseInternal(s, out SnowflakeId result)) {
            return result;
        }
        throw new FormatException($"'{s}' is not a valid SnowflakeId.");
    }

    /// <summary>
    /// Tries to parse a span of characters into a <see cref="SnowflakeId"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out SnowflakeId result) {
        return TryParseInternal(s, out result);
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="SnowflakeId"/>.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 encoded text to parse.</param>
    /// <returns>The parsed SnowflakeId.</returns>
    /// <exception cref="FormatException">Thrown if the input is not in a valid format.</exception>
    public static SnowflakeId Parse(ReadOnlySpan<byte> utf8Text) {
        if (TryParseInternal(utf8Text, out SnowflakeId result)) {
            return result;
        }
        throw new FormatException("The input is not a valid UTF-8 encoded SnowflakeId.");
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="SnowflakeId"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out SnowflakeId result) {
        return TryParseInternal(utf8Text, out result);
    }

    // --- Internal Logic ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseInternal(ReadOnlySpan<char> s, out SnowflakeId result) {
        // Fast path: Try parsing as a pure long integer.
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longVal)) {
            result = new SnowflakeId(longVal);
            return true;
        }

        // Fallback: Check if it's a Guid string (compatibility mode).
        if (Guid.TryParse(s, out Guid guidVal)) {
            result = FromGuid(guidVal);
            return true;
        }

        result = Empty;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseInternal(ReadOnlySpan<byte> utf8Text, out SnowflakeId result) {
        // High-performance UTF-8 parsing without string allocation.
        if (Utf8Parser.TryParse(utf8Text, out long longVal, out int bytesConsumed) && bytesConsumed == utf8Text.Length) {
            result = new SnowflakeId(longVal);
            return true;
        }

        // We don't support Guid parsing from raw UTF-8 bytes here for simplicity and speed,
        // but it could be added if necessary.
        result = Empty;
        return false;
    }

    // --- Explicit Interface Implementations ---

    static SnowflakeId IParsable<SnowflakeId>.Parse(string s, IFormatProvider? provider) {
        return Parse(s.AsSpan());
    }

    static bool IParsable<SnowflakeId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SnowflakeId result) {
        if (s is null) { result = default; return false; }
        return TryParseInternal(s.AsSpan(), out result);
    }

    static SnowflakeId ISpanParsable<SnowflakeId>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<SnowflakeId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SnowflakeId result) {
        return TryParse(s, out result);
    }

    static SnowflakeId IUtf8SpanParsable<SnowflakeId>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<SnowflakeId>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out SnowflakeId result) {
        return TryParse(utf8Text, out result);
    }

    #endregion

    #region Formatting

    /// <inheritdoc/>
    public override string ToString() {
        return this._value.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return this._value.ToString(format, formatProvider);
    }

    /// <inheritdoc/>
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return this._value.TryFormat(destination, out charsWritten, format, provider);
    }

    /// <inheritdoc/>
    bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return this._value.TryFormat(utf8Destination, out bytesWritten, format, provider);
    }

    /// <summary>
    /// Writes the 64-bit ID (8 bytes) in Big Endian format to the provided buffer writer.
    /// Highly optimized for binary serialization (e.g., Kestrel, gRPC).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteTo(IBufferWriter<byte> writer) {
        Span<byte> buffer = writer.GetSpan(8);
        BinaryPrimitives.WriteInt64BigEndian(buffer, this._value);
        writer.Advance(8);
    }

    #endregion

    #region Conversions (Guid, Bytes, Int128)

    /// <summary>
    /// Converts the SnowflakeId to a Guid. 
    /// The 64-bit ID is placed in the last 8 bytes of the Guid (Big Endian).
    /// The first 8 bytes are zeroed out.
    /// </summary>
    public Guid ToGuid() {
        if (this._value == 0) {
            return Guid.Empty;
        }

        Span<byte> guidBytes = stackalloc byte[16];
        // Ensure first 8 bytes are zero.
        guidBytes[..8].Clear();
        BinaryPrimitives.WriteInt64BigEndian(guidBytes[8..], this._value);

        return new Guid(guidBytes);
    }

    /// <summary>
    /// Reconstructs a SnowflakeId from a Guid created via <see cref="ToGuid"/>.
    /// </summary>
    public static SnowflakeId FromGuid(Guid guid) {
        if (guid == Guid.Empty) {
            return Empty;
        }

        Span<byte> guidBytes = stackalloc byte[16];
        guid.TryWriteBytes(guidBytes);
        long val = BinaryPrimitives.ReadInt64BigEndian(guidBytes[8..]);
        return new SnowflakeId(val);
    }

    /// <summary>
    /// Creates a byte array (8 bytes) representing the ID (Big Endian).
    /// </summary>
    public byte[] ToByteArray() {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, this._value);
        return bytes;
    }

    /// <summary>
    /// Writes the 64-bit ID into a span of bytes (Big Endian).
    /// </summary>
    public bool TryWriteBytes(Span<byte> destination) {
        if (destination.Length < 8) {
            return false;
        }

        BinaryPrimitives.WriteInt64BigEndian(destination, this._value);
        return true;
    }

    /// <summary>
    /// Reads a SnowflakeId from a span of bytes (Big Endian).
    /// </summary>
    public static SnowflakeId FromBytes(ReadOnlySpan<byte> source) {
        if (source.Length < 8) {
            throw new ArgumentException("Source must be at least 8 bytes.", nameof(source));
        }

        return new SnowflakeId(BinaryPrimitives.ReadInt64BigEndian(source));
    }

    // --- Operators ---

    /// <summary>
    /// Implicitly converts a 64-bit signed integer to a <see cref="SnowflakeId"/>.
    /// </summary>
    /// <param name="value">The long value.</param>
    public static implicit operator SnowflakeId(long value) {
        return new(value);
    }

    /// <summary>
    /// Implicitly converts a <see cref="SnowflakeId"/> to its underlying 64-bit signed integer value.
    /// </summary>
    /// <param name="id">The SnowflakeId.</param>
    public static implicit operator long(SnowflakeId id) {
        return id._value;
    }

    /// <summary>
    /// Explicitly converts a <see cref="Guid"/> to a <see cref="SnowflakeId"/>.
    /// </summary>
    /// <remarks>
    /// This operation extracts the last 8 bytes of the Guid (Big Endian) to form the ID. 
    /// Data loss may occur if the Guid was not originally created from a SnowflakeId.
    /// </remarks>
    /// <param name="guid">The source Guid.</param>
    public static explicit operator SnowflakeId(Guid guid) {
        return FromGuid(guid);
    }

    /// <summary>
    /// Implicitly converts a <see cref="SnowflakeId"/> to a <see cref="Guid"/>.
    /// </summary>
    /// <remarks>
    /// The resulting Guid will have its first 8 bytes set to zero, and the last 8 bytes will contain the SnowflakeId.
    /// </remarks>
    /// <param name="id">The SnowflakeId.</param>
    public static implicit operator Guid(SnowflakeId id) {
        return id.ToGuid();
    }

    /// <summary>
    /// Implicitly converts a <see cref="SnowflakeId"/> to an <see cref="Int128"/>.
    /// </summary>
    /// <remarks>
    /// The SnowflakeId value is placed in the lower 64 bits of the Int128.
    /// </remarks>
    /// <param name="id">The SnowflakeId.</param>
    public static implicit operator Int128(SnowflakeId id) {
        return (Int128)id._value;
    }

    /// <summary>
    /// Converts the <see cref="SnowflakeId"/> to an <see cref="Int128"/>.
    /// </summary>
    /// <returns>An Int128 containing the SnowflakeId in its lower 64 bits.</returns>
    public Int128 ToInt128() {
        return (Int128)this._value;
    }

    #endregion

    #region Integration (Hex, Base64, Base32, Base62, Urn)

    /// <summary>
    /// Converts the SnowflakeId to a type-safe HexString.
    /// </summary>
    public HexString ToHexString() {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, this._value);
        return HexString.FromBytes(buffer);
    }

    /// <summary>
    /// Converts the SnowflakeId to a type-safe Base64String.
    /// </summary>
    public Base64String ToBase64String() {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, this._value);
        return Base64String.FromBytes(buffer);
    }

    /// <summary>
    /// Converts the SnowflakeId to a type-safe Base62String.
    /// This is ideal for URL shortening (e.g., "3k7Za").
    /// </summary>
    public Base62String ToBase62String() {
        return Base62String.FromInt64(this._value);
    }

    /// <summary>
    /// Converts the SnowflakeId to a type-safe Base32String.
    /// </summary>
    public Base32String ToBase32String() {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, this._value);
        return Base32String.FromBytes(buffer);
    }

    /// <summary>
    /// Creates a URN using this ID and a specified namespace.
    /// </summary>
    public Urn ToUrn(string nid) {
        return Urn.Create(nid, this);
    }

    /// <summary>
    /// Creates a <see cref="SnowflakeId"/> from a <see cref="HexString"/>.
    /// </summary>
    /// <param name="hex">The hexadecimal string representation to convert.</param>
    /// <returns>A new <see cref="SnowflakeId"/> derived from the hex string.</returns>
    /// <exception cref="FormatException">Thrown if the hex string does not represent exactly 8 bytes (64 bits).</exception>
    public static SnowflakeId From(HexString hex) {
        Span<byte> buffer = stackalloc byte[8];
        if (!hex.TryDecode(buffer, out int written) || written != 8) {
            throw new FormatException("HexString must correspond to exactly 8 bytes.");
        }

        return FromBytes(buffer);
    }

    /// <summary>
    /// Creates a <see cref="SnowflakeId"/> from a <see cref="Base64String"/>.
    /// </summary>
    /// <param name="base64">The Base64 string representation to convert.</param>
    /// <returns>A new <see cref="SnowflakeId"/> derived from the Base64 string.</returns>
    /// <exception cref="FormatException">Thrown if the Base64 string does not represent exactly 8 bytes (64 bits).</exception>
    public static SnowflakeId From(Base64String base64) {
        Span<byte> buffer = stackalloc byte[8];
        if (!base64.TryDecode(buffer, out int written) || written != 8) {
            throw new FormatException("Base64String must correspond to exactly 8 bytes.");
        }

        return FromBytes(buffer);
    }

    /// <summary>
    /// Creates a <see cref="SnowflakeId"/> from a <see cref="Base32String"/>.
    /// </summary>
    /// <param name="base32">The Base32 string representation to convert.</param>
    /// <returns>A new <see cref="SnowflakeId"/> derived from the Base32 string.</returns>
    /// <exception cref="FormatException">Thrown if the Base32 string does not represent exactly 8 bytes (64 bits).</exception>
    public static SnowflakeId From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[8];
        if (!base32.TryDecode(buffer, out int written) || written != 8) {
            throw new FormatException("Base32String must correspond to exactly 8 bytes.");
        }

        return FromBytes(buffer);
    }

    #endregion

    #region Equality & Comparison
    /// <inheritdoc/>
    public bool Equals(SnowflakeId other) {
        return this._value == other._value;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        return obj is SnowflakeId other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        return this._value.GetHashCode();
    }

    /// <inheritdoc/>
    public int CompareTo(SnowflakeId other) {
        return this._value.CompareTo(other._value);
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) {
        if (obj is null) {
            return 1;
        }

        if (obj is SnowflakeId other) {
            return CompareTo(other);
        }

        throw new ArgumentException($"Object must be of type {nameof(SnowflakeId)}", nameof(obj));
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(SnowflakeId left, SnowflakeId right) {
        return left._value == right._value;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(SnowflakeId left, SnowflakeId right) {
        return left._value != right._value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    public static bool operator <(SnowflakeId left, SnowflakeId right) {
        return left._value < right._value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    public static bool operator >(SnowflakeId left, SnowflakeId right) {
        return left._value > right._value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    public static bool operator <=(SnowflakeId left, SnowflakeId right) {
        return left._value <= right._value;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    public static bool operator >=(SnowflakeId left, SnowflakeId right) {
        return left._value >= right._value;
    }

    #endregion
}