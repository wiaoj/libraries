using System.Buffers;
using System.Buffers.Binary;
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
/// This struct is a 64-bit integer wrapper designed for high-performance, sorting, and database efficiency.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SnowflakeId"/> struct.
/// </remarks>
/// <param name="value">The 64-bit integer value.</param>
[TypeConverter(typeof(SnowflakeIdTypeConverter))]
[JsonConverter(typeof(SnowflakeIdJsonConverter))]
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public readonly struct SnowflakeId(long value) :
    IEquatable<SnowflakeId>,
    IComparable<SnowflakeId>,
    IComparable,
    ISpanFormattable,
    ISpanParsable<SnowflakeId>,
    IUtf8SpanFormattable,
    IEqualityOperators<SnowflakeId, SnowflakeId, bool>,
    IComparisonOperators<SnowflakeId, SnowflakeId, bool> {

    private static SnowflakeGenerator _sharedGenerator = SnowflakeGenerator.Default;
    private static readonly Lock _configLock = new();

    /// <summary>
    /// Configures the global shared generator using a complete options object.
    /// This gives full control over SequenceBits, MaxDriftMs, Epoch, etc.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public static void Configure(SnowflakeOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        lock (_configLock) {
            _sharedGenerator = new SnowflakeGenerator(options);
        }
    }

    /// <summary>
    /// Configures the global shared generator with a specific Node ID using default Epoch (2024-01-01).
    /// </summary>
    /// <param name="nodeId">The unique ID of this machine/node.</param>
    public static void Configure(ushort nodeId) {
        Configure(new SnowflakeOptions { NodeId = nodeId });
    }

    /// <summary>
    /// Configures the global shared generator with a specific Node ID and Epoch.
    /// </summary>
    /// <param name="nodeId">The unique ID of this machine/node.</param>
    /// <param name="epoch">The start date for the ID generation.</param>
    public static void Configure(ushort nodeId, DateTimeOffset epoch) {
        Configure(new SnowflakeOptions { NodeId = nodeId, Epoch = epoch });
    }
    /// <summary>
    /// Generates a new unique <see cref="SnowflakeId"/> using the shared global configuration.
    /// This method is thread-safe, lock-free on the hot path, and allocation-free.
    /// </summary>
    /// <returns>A new unique SnowflakeId.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static SnowflakeId NewId() {
        return _sharedGenerator.NextId();
    }

    private readonly long _value = value;

    /// <summary>
    /// Represents an empty or zero SnowflakeId.
    /// </summary>
    public static readonly SnowflakeId Empty = new(0);

    /// <summary>
    /// Gets the underlying 64-bit integer value of the ID.
    /// </summary>
    public long Value => this._value;

    // -------------------------------------------------------------------------
    // BUFFER WRITER SUPPORT (NEW)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Writes the 64-bit ID (8 bytes) in Big Endian format to the provided buffer writer.
    /// This method is highly optimized for network serialization (e.g. Kestrel).
    /// </summary>
    /// <param name="writer">The target buffer writer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteTo(IBufferWriter<byte> writer) {
        Span<byte> buffer = writer.GetSpan(8);
        BinaryPrimitives.WriteInt64BigEndian(buffer, this._value);
        writer.Advance(8);
    }

    /// <summary>
    /// Converts the SnowflakeId to a Guid. 
    /// The 64-bit ID is placed in the last 8 bytes of the Guid (Big Endian).
    /// The first 8 bytes are padded with zeros.
    /// </summary>
    public Guid ToGuid() {
        if (this._value == 0) {
            return Guid.Empty;
        }

        // SkipLocalsInit prevents zero-initialization of this stack block.
        // We must manually clear the first 8 bytes to ensure consistent Guid format.
        Span<byte> guidBytes = stackalloc byte[16];
        guidBytes[..8].Clear();
        BinaryPrimitives.WriteInt64BigEndian(guidBytes[8..], this._value);

        return new Guid(guidBytes);
    }

    /// <summary>
    /// Reconstructs a SnowflakeId from a Guid that was created via <see cref="ToGuid"/>.
    /// Warning: Guids not created by SnowflakeId will result in data loss or invalid IDs.
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

    /// <inheritdoc/>
    public override string ToString() {
        return this._value.ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) {
        return this._value.ToString(format, formatProvider);
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return this._value.TryFormat(destination, out charsWritten, format, provider);
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        return this._value.TryFormat(utf8Destination, out bytesWritten, format, provider);
    }

    /// <summary>
    /// Converts the string representation of a number or Guid to its SnowflakeId equivalent.
    /// </summary>
    public static SnowflakeId Parse(string s, IFormatProvider? provider) {
        return Parse(s.AsSpan(), provider);
    }

    /// <summary>
    /// Converts the string representation of a number or Guid to its SnowflakeId equivalent.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out SnowflakeId result) {
        return TryParse(s.AsSpan(), provider, out result);
    }

    /// <summary>
    /// Converts the span representation of a number or Guid to its SnowflakeId equivalent.
    /// </summary>
    public static SnowflakeId Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        if (TryParse(s, provider, out SnowflakeId result)) {
            return result;
        }

        throw new FormatException("Invalid SnowflakeId format. Expected a numeric string or a Guid string.");
    }

    /// <summary>
    /// Converts the span representation of a number or Guid to its SnowflakeId equivalent.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out SnowflakeId result) {
        // 1. Try parsing as Long (Standard usage) - Fastest
        if (long.TryParse(s, NumberStyles.Integer, provider, out var longVal)) {
            result = new SnowflakeId(longVal);
            return true;
        }
        // 2. Try parsing as Guid (Compatibility usage)
        if (Guid.TryParse(s, provider, out Guid guidVal)) {
            result = FromGuid(guidVal);
            return true;
        }

        result = Empty;
        return false;
    }

    /// <summary>Implicitly converts a <see cref="long"/> value to a <see cref="SnowflakeId"/>.</summary>
    public static implicit operator SnowflakeId(long value) {
        return new(value);
    }

    /// <summary>Implicitly converts a <see cref="SnowflakeId"/> to its underlying <see cref="long"/> value.</summary>
    public static implicit operator long(SnowflakeId id) {
        return id._value;
    }

    /// <summary>Explicitly converts a <see cref="Guid"/> to a <see cref="SnowflakeId"/>.</summary>
    public static explicit operator SnowflakeId(Guid guid) {
        return FromGuid(guid);
    }

    /// <summary>Implicitly converts a <see cref="SnowflakeId"/> to a <see cref="Guid"/>.</summary>
    public static implicit operator Guid(SnowflakeId id) {
        return id.ToGuid();
    }

    /// <summary>
    /// Converts the SnowflakeId to an Int128. The value is placed in the lower 64 bits.
    /// </summary>
    public Int128 ToInt128() {
        return (Int128)this._value;
    }

    /// <summary>Implicitly converts a <see cref="SnowflakeId"/> to an <see cref="Int128"/>.</summary>
    public static implicit operator Int128(SnowflakeId id) {
        return id.ToInt128();
    }

    /// <summary>
    /// Writes the 64-bit ID into a span of bytes (Big Endian).
    /// Requires 8 bytes.
    /// </summary>
    public bool TryWriteBytes(Span<byte> destination) {
        if (destination.Length < 8) {
            return false;
        }

        BinaryPrimitives.WriteInt64BigEndian(destination, this._value);
        return true;
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
    /// Reads a SnowflakeId from a span of bytes (Big Endian).
    /// </summary>
    public static SnowflakeId FromBytes(ReadOnlySpan<byte> source) {
        if (source.Length < 8) {
            throw new ArgumentException("Source must be at least 8 bytes.", nameof(source));
        }

        return new SnowflakeId(BinaryPrimitives.ReadInt64BigEndian(source));
    }

    // -------------------------------------------------------------------------
    // INTEGRATION: Hex, Base64, Base32
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts the SnowflakeId to a type-safe HexString.
    /// Useful for debugging and logging.
    /// </summary>
    public HexString ToHexString() {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, this._value);
        return HexString.FromBytes(buffer);
    }

    /// <summary>
    /// Converts the SnowflakeId to a type-safe Base64String.
    /// Useful for compact storage or JSON transmission.
    /// </summary>
    public Base64String ToBase64String() {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, this._value);
        return Base64String.FromBytes(buffer);
    }

    /// <summary>
    /// Converts the SnowflakeId to a type-safe Base32String.
    /// Useful for case-insensitive, URL-safe identifiers (shorter than UUID).
    /// </summary>
    public Base32String ToBase32String() {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, this._value);
        return Base32String.FromBytes(buffer);
    }

    /// <summary>
    /// Creates a URN using this ID and a specified namespace.
    /// Example: "urn:order:123456"
    /// </summary>
    public Urn ToUrn(string nid) {
        return Urn.Create(nid, this);
    }


    /// <summary>
    /// Creates a SnowflakeId from a HexString.
    /// </summary>
    public static SnowflakeId From(HexString hex) {
        if (hex.GetDecodedLength() != 8) {
            throw new FormatException("HexString for SnowflakeId must represent exactly 8 bytes.");
        }

        Span<byte> buffer = stackalloc byte[8];
        hex.TryDecode(buffer, out _);
        return FromBytes(buffer);
    }

    /// <summary>
    /// Creates a SnowflakeId from a Base64String.
    /// </summary>
    public static SnowflakeId From(Base64String base64) {
        if (base64.GetDecodedLength() != 8) {
            throw new FormatException("Base64String for SnowflakeId must represent exactly 8 bytes.");
        }

        Span<byte> buffer = stackalloc byte[8];
        base64.TryDecode(buffer, out _);
        return FromBytes(buffer);
    }

    /// <summary>
    /// Creates a SnowflakeId from a Base32String.
    /// </summary>
    public static SnowflakeId From(Base32String base32) {
        if (base32.GetDecodedLength() != 8) {
            throw new FormatException("Base32String for SnowflakeId must represent exactly 8 bytes.");
        }

        Span<byte> buffer = stackalloc byte[8];
        base32.TryDecode(buffer, out _);
        return FromBytes(buffer);
    }

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

        throw new ArgumentException("Object is not a SnowflakeId");
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Equality(TSelf, TOther)" />
    public static bool operator ==(SnowflakeId left, SnowflakeId right) {
        return left.Equals(right);
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.op_Inequality(TSelf, TOther)" />
    public static bool operator !=(SnowflakeId left, SnowflakeId right) {
        return !left.Equals(right);
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
}