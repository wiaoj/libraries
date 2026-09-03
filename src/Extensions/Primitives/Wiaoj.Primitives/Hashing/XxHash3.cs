using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.Hashing.Internal;
using Wiaoj.Primitives.JsonConverters;

namespace Wiaoj.Primitives.Hashing;

/// <summary>
/// Represents an immutable, fixed-size 8-byte (64-bit) XXHash3 hash.
/// </summary>
/// <remarks>
/// <para>
/// <b>Non-Cryptographic:</b> XXHash3 is an extremely fast, state-of-the-art non-cryptographic hash algorithm designed for 
/// data deduplication, in-memory hash tables, cache keys, and checksums.
/// </para>
/// <para>
/// <b>Zero Heap Allocation:</b> The hash is stored internally as a single 64-bit unsigned integer (<see cref="ulong"/>), 
/// requiring no unsafe pointer buffers or heap allocations.
/// </para>
/// </remarks>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Sequential)]
[JsonConverter(typeof(XxHash3JsonConverter))]
[SkipLocalsInit]
public readonly struct XxHash3
    : IEquatable<XxHash3>,
      IComparable<XxHash3>,
      IComparable,
      IParsable<XxHash3>,
      ISpanParsable<XxHash3>,
      IUtf8SpanParsable<XxHash3>,
      ISpanFormattable,
      IUtf8SpanFormattable,
      IFormattable,
      IEqualityOperators<XxHash3, XxHash3, bool>,
      IComparisonOperators<XxHash3, XxHash3, bool> {

    /// <summary>The size of the XXHash3-64 hash in bytes (8 bytes / 64 bits).</summary>
    internal const int HashSizeInBytes = 8;

    /// <summary>The size of the XXHash3-64 hash in bytes (8 bytes / 64 bits).</summary>
    public const int SizeInBytes = HashSizeInBytes;

    private readonly ulong _value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal XxHash3(ulong value) {
        this._value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XxHash3"/> struct from an 8-byte span.
    /// </summary>
    /// <param name="source">A span containing exactly 8 bytes of hash data.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is not exactly 8 bytes long.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal XxHash3(ReadOnlySpan<byte> source) {
        if(source.Length != HashSizeInBytes) {
            throw new ArgumentException($"Source span must be exactly {HashSizeInBytes} bytes long.", nameof(source));
        }
        this._value = MemoryMarshal.Read<ulong>(source);
    }

    #region Factory Methods

    /// <summary>
    /// Represents an empty (zero-filled) 8-byte <see cref="XxHash3"/> hash.
    /// </summary>
    public static readonly XxHash3 Empty = default;

    /// <summary>
    /// Gets the raw 64-bit unsigned integer hash value.
    /// </summary>
    public ulong Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._value;
    }

    /// <summary>
    /// Creates an <see cref="XxHash3"/> instance from an 8-byte read-only span.
    /// </summary>
    /// <param name="source">A span containing exactly 8 bytes of hash data.</param>
    /// <returns>A valid <see cref="XxHash3"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is not exactly 8 bytes long.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 FromBytes(ReadOnlySpan<byte> source) {
        return new(source);
    }

    /// <summary>
    /// Creates an <see cref="XxHash3"/> instance from a hexadecimal string representation.
    /// </summary>
    /// <param name="hex">The hex-encoded string representing the 8-byte hash (16 hex characters).</param>
    /// <returns>A new <see cref="XxHash3"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="hex"/> does not decode to exactly 8 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 From(HexString hex) {
        if(hex.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException($"Source HexString must represent exactly {HashSizeInBytes} bytes (16 hex characters).");
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        return new(buffer);
    }

    /// <summary>
    /// Creates an <see cref="XxHash3"/> instance from a Base64-encoded string.
    /// </summary>
    /// <param name="base64">The Base64-encoded string representing the 8-byte hash.</param>
    /// <returns>A new <see cref="XxHash3"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base64"/> does not decode to exactly 8 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 From(Base64String base64) {
        if(base64.GetDecodedLength() != HashSizeInBytes) {
            throw new FormatException($"Source Base64String must represent exactly {HashSizeInBytes} bytes.");
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(!base64.TryDecode(buffer, out int written) || written != HashSizeInBytes) {
            throw new FormatException("Failed to decode Base64 into Hash.");
        }
        return new(buffer);
    }

    /// <summary>
    /// Creates an <see cref="XxHash3"/> instance from a Base32-encoded string.
    /// </summary>
    /// <param name="base32">The Base32-encoded string representing the 8-byte hash.</param>
    /// <returns>A new <see cref="XxHash3"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base32"/> does not decode to exactly 8 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 From(Base32String base32) {
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        if(base32.TryDecode(buffer, out int written) && written == HashSizeInBytes) {
            return new(buffer);
        }
        throw new FormatException($"Base32 string length mismatch for {HashSizeInBytes}-byte hash.");
    }

    /// <summary>
    /// Creates an <see cref="XxHash3"/> instance from a Base62-encoded string.
    /// </summary>
    /// <param name="base62">The Base62-encoded string representing the 8-byte hash.</param>
    /// <returns>A new <see cref="XxHash3"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="base62"/> represents a value exceeding 8 bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 From(Base62String base62) {
        byte[] bytes = base62.ToBytes();
        if(bytes.Length > HashSizeInBytes) {
            for(int i = 0; i < bytes.Length - HashSizeInBytes; i++) {
                if(bytes[i] != 0) {
                    throw new FormatException("Base62 string represents a value too large for this hash.");
                }
            }
            return new(bytes.AsSpan(bytes.Length - HashSizeInBytes));
        }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        buffer.Clear();
        bytes.CopyTo(buffer[(HashSizeInBytes - bytes.Length)..]);
        return new(buffer);
    }

    /// <summary>
    /// Parses a 16-character hexadecimal string into an <see cref="XxHash3"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Parse(string s) {
        Preca.ThrowIfNull(s);
        if(!TryParse(s.AsSpan(), out XxHash3 result)) {
            throw new FormatException($"Input string must represent exactly {HashSizeInBytes} bytes (16 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a 16-character hexadecimal span into an <see cref="XxHash3"/> without heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Parse(ReadOnlySpan<char> s) {
        if(!TryParse(s, out XxHash3 result)) {
            throw new FormatException($"Input span must represent exactly {HashSizeInBytes} bytes (16 hex characters).");
        }
        return result;
    }

    /// <summary>
    /// Parses a UTF-8 encoded hexadecimal byte span into an <see cref="XxHash3"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Parse(ReadOnlySpan<byte> utf8Text) {
        if(!TryParse(utf8Text, out XxHash3 result)) {
            throw new FormatException("Invalid UTF-8 hexadecimal sequence for XxHash3.");
        }
        return result;
    }

    /// <summary>
    /// Attempts to parse a hexadecimal string into an <see cref="XxHash3"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse([NotNullWhen(true)] string? s, out XxHash3 result) {
        if(s is null) { result = default; return false; }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a hexadecimal span into an <see cref="XxHash3"/> without heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> s, out XxHash3 result) {
        if(HexString.TryParse(s, out HexString hex)) return TryParse(hex, out result);
        result = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse a UTF-8 encoded byte span into an <see cref="XxHash3"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out XxHash3 result) {
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
    /// Attempts to parse a <see cref="HexString"/> into an <see cref="XxHash3"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(HexString hex, out XxHash3 result) {
        if(hex.GetDecodedLength() != HashSizeInBytes) { result = default; return false; }
        Span<byte> buffer = stackalloc byte[HashSizeInBytes];
        hex.TryDecode(buffer, out _);
        result = new(buffer);
        return true;
    }

    #endregion

    #region Explicit Interface Implementations (IParsable, ISpanParsable, IUtf8SpanParsable)

    static XxHash3 IParsable<XxHash3>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<XxHash3>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out XxHash3 result) {
        return TryParse(s, out result);
    }

    static XxHash3 ISpanParsable<XxHash3>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<XxHash3>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out XxHash3 result) {
        return TryParse(s, out result);
    }

    static XxHash3 IUtf8SpanParsable<XxHash3>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<XxHash3>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out XxHash3 result) {
        return TryParse(utf8Text, out result);
    }

    #endregion

    #region High-Performance Computation

    /// <summary>
    /// Computes the XXHash3-64 hash for the contents of a secure <see cref="Secret{Byte}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(Secret<byte> secret) {
        Preca.ThrowIfNull(secret);
        return secret.Expose(span => Compute(span));
    }

    /// <summary>
    /// Computes the XXHash3-64 hash for the contents of a secure <see cref="Secret{Byte}"/> using the specified seed.
    /// </summary>
    /// <param name="secret">The secret byte data to hash.</param>
    /// <param name="seed">The seed to initialize the hash computation with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(Secret<byte> secret, long seed) {
        Preca.ThrowIfNull(secret);
        return secret.Expose(span => Compute(span, seed));
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a byte span using SIMD hardware acceleration without heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static XxHash3 Compute(ReadOnlySpan<byte> data) {
        return new(XxHash3Core.HashToUInt64(data));
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a byte span using SIMD hardware acceleration without heap allocations,
    /// using the specified seed.
    /// </summary>
    /// <param name="data">The byte span to hash.</param>
    /// <param name="seed">The seed to initialize the hash computation with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static XxHash3 Compute(ReadOnlySpan<byte> data, long seed) {
        return new(XxHash3Core.HashToUInt64(data, seed));
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a character span using UTF-8 encoding.
    /// </summary>
    /// <param name="chars">The character span to hash.</param>
    /// <returns>A new <see cref="XxHash3"/> instance containing the 64-bit digest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(ReadOnlySpan<char> chars) {
        return Compute(chars, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a character span using UTF-8 encoding and the specified seed.
    /// </summary>
    /// <param name="chars">The character span to hash.</param>
    /// <param name="seed">The seed to initialize the hash computation with.</param>
    /// <returns>A new <see cref="XxHash3"/> instance containing the 64-bit digest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(ReadOnlySpan<char> chars, long seed) {
        return Compute(chars, Encoding.UTF8, seed);
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a character span using the specified encoding.
    /// </summary>
    /// <param name="chars">The character span to hash.</param>
    /// <param name="encoding">The encoding used to convert the characters to bytes before hashing.</param>
    /// <returns>A new <see cref="XxHash3"/> instance containing the 64-bit digest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static XxHash3 Compute(ReadOnlySpan<char> chars, Encoding encoding) {
        Preca.ThrowIfNull(encoding);
        int maxByteCount = encoding.GetMaxByteCount(chars.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
        int bytesWritten = encoding.GetBytes(chars, buffer.Span);
        return Compute(buffer.Span[..bytesWritten]);
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a character span using the specified encoding and seed.
    /// </summary>
    /// <param name="chars">The character span to hash.</param>
    /// <param name="encoding">The encoding used to convert the characters to bytes before hashing.</param>
    /// <param name="seed">The seed to initialize the hash computation with.</param>
    /// <returns>A new <see cref="XxHash3"/> instance containing the 64-bit digest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static XxHash3 Compute(ReadOnlySpan<char> chars, Encoding encoding, long seed) {
        Preca.ThrowIfNull(encoding);
        int maxByteCount = encoding.GetMaxByteCount(chars.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
        int bytesWritten = encoding.GetBytes(chars, buffer.Span);
        return Compute(buffer.Span[..bytesWritten], seed);
    }

    /// <summary>
    /// Computes the XXHash3-64 hash for the contents of a secure <see cref="Secret{Char}"/> using the specified encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    /// Computes the XXHash3-64 hash for the contents of a secure <see cref="Secret{Char}"/> using the specified encoding and seed.
    /// </summary>
    /// <param name="secret">The secret character data to hash.</param>
    /// <param name="encoding">The encoding used to convert the characters to bytes before hashing.</param>
    /// <param name="seed">The seed to initialize the hash computation with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(Secret<char> secret, Encoding encoding, long seed) {
        Preca.ThrowIfNull(secret);
        Preca.ThrowIfNull(encoding);
        return secret.Expose(chars => {
            int maxByteCount = encoding.GetMaxByteCount(chars.Length);
            using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
            int bytesWritten = encoding.GetBytes(chars, buffer.Span);
            return Compute(buffer.Span[..bytesWritten], seed);
        });
    }

    /// <summary>
    /// Computes the XXHash3-64 hash for the contents of a secure <see cref="Secret{Char}"/> using UTF-8 encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(Secret<char> secret) {
        return Compute(secret, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the XXHash3-64 hash for the contents of a secure <see cref="Secret{Char}"/> using UTF-8 encoding and the specified seed.
    /// </summary>
    /// <param name="secret">The secret character data to hash.</param>
    /// <param name="seed">The seed to initialize the hash computation with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(Secret<char> secret, long seed) {
        return Compute(secret, Encoding.UTF8, seed);
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a string using the specified encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    /// Computes the XXHash3-64 hash of a string using the specified encoding and seed.
    /// </summary>
    /// <param name="text">The string to hash.</param>
    /// <param name="encoding">The encoding used to convert the string to bytes before hashing.</param>
    /// <param name="seed">The seed to initialize the hash computation with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SkipLocalsInit]
    public static XxHash3 Compute(string text, Encoding encoding, long seed) {
        Preca.ThrowIfNull(text);
        Preca.ThrowIfNull(encoding);
        int maxByteCount = encoding.GetMaxByteCount(text.Length);
        using ValueBuffer<byte> buffer = new(maxByteCount, stackalloc byte[1024]);
        int bytesWritten = encoding.GetBytes(text, buffer.Span);
        return Compute(buffer.Span[..bytesWritten], seed);
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a string using UTF-8 encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(string text) {
        return Compute(text, Encoding.UTF8);
    }

    /// <summary>
    /// Computes the XXHash3-64 hash of a string using UTF-8 encoding and the specified seed.
    /// </summary>
    /// <param name="text">The string to hash.</param>
    /// <param name="seed">The seed to initialize the hash computation with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(string text, long seed) {
        return Compute(text, Encoding.UTF8, seed);
    }

    /// <summary>
    /// Computes the XXHash3-64 hash by streaming data written to an <see cref="IBufferWriter{Byte}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The underlying writer and its pooled buffer are cached per-thread (<see cref="ThreadStaticAttribute"/>).
    /// After the first call on a given thread, subsequent (non-reentrant) calls on that same thread perform
    /// <b>zero heap allocations</b> for the writer/buffer machinery itself.
    /// </para>
    /// <para>
    /// Reentrant usage (calling <see cref="Compute{TState}"/> again from within <paramref name="writeAction"/>
    /// on the same thread) remains fully correct: the nested call transparently falls back to renting a fresh
    /// writer instead of reusing the one that is already checked out.
    /// </para>
    /// </remarks>
    public static XxHash3 Compute<TState>(TState state, Action<IBufferWriter<byte>, TState> writeAction) {
        Preca.ThrowIfNull(writeAction);

        StreamingXxHash3Writer writer = StreamingXxHash3Writer.Rent();
        try {
            writeAction(writer, state);
            return new(writer.GetCurrentHashAsUInt64());
        }
        finally {
            StreamingXxHash3Writer.Return(writer);
        }
    }

    /// <summary>
    /// Computes the XXHash3-64 hash by streaming data written to an <see cref="IBufferWriter{Byte}"/>,
    /// using the specified seed.
    /// </summary>
    /// <param name="state">A user-defined state object passed through to <paramref name="writeAction"/>.</param>
    /// <param name="writeAction">The delegate that writes the data to be hashed.</param>
    /// <param name="seed">The seed to initialize the hash computation with.</param>
    public static XxHash3 Compute<TState>(TState state, Action<IBufferWriter<byte>, TState> writeAction, long seed) {
        Preca.ThrowIfNull(writeAction);

        StreamingXxHash3Writer writer = StreamingXxHash3Writer.Rent(seed);
        try {
            writeAction(writer, state);
            return new(writer.GetCurrentHashAsUInt64());
        }
        finally {
            StreamingXxHash3Writer.Return(writer);
        }
    }

    /// <summary>
    /// Convenience overload for parameterless or static write actions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(Action<IBufferWriter<byte>> writeAction) {
        Preca.ThrowIfNull(writeAction);
        return Compute(writeAction, static (writer, act) => act(writer));
    }

    /// <summary>
    /// Convenience overload for parameterless or static write actions, using the specified seed.
    /// </summary>
    /// <param name="writeAction">The delegate that writes the data to be hashed.</param>
    /// <param name="seed">The seed to initialize the hash computation with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XxHash3 Compute(Action<IBufferWriter<byte>> writeAction, long seed) {
        Preca.ThrowIfNull(writeAction);
        return Compute(writeAction, static (writer, act) => act(writer), seed);
    }

    #endregion

    #region Data Access & Conversion

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{Byte}"/> view of the 8 hash bytes without heap allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<byte> AsSpan() {
        return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this._value), 1));
    }

    /// <summary>
    /// Provides safe, scoped access to the hash bytes as a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Expose(Action<ReadOnlySpan<byte>> action) {
        action(AsSpan());
    }

    /// <summary>
    /// Provides safe, scoped access to the hash bytes and returns a result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly TResult Expose<TResult>(Func<ReadOnlySpan<byte>, TResult> func) {
        return func(AsSpan());
    }

    /// <summary>
    /// Copies the hash bytes into a destination span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) {
            throw new ArgumentException($"Destination span must be at least {HashSizeInBytes} bytes long.", nameof(destination));
        }
        AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Attempts to copy the hash bytes into the specified destination span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryCopyTo(Span<byte> destination) {
        if(destination.Length < HashSizeInBytes) return false;
        AsSpan().CopyTo(destination);
        return true;
    }

    /// <summary>
    /// Encodes the hash bytes into an uppercase <see cref="HexString"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexString ToHexString() {
        return HexString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a lowercase <see cref="HexString"/> without string allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HexString ToHexStringLower() {
        return HexString.FromBytesLower(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base64String"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Base64String ToBase64String() {
        return Base64String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe, URL-safe <see cref="Base64UrlString"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Base64UrlString ToBase64UrlString() {
        return Base64UrlString.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base32String"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Base32String ToBase32String() {
        return Base32String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Encodes the hash bytes into a type-safe <see cref="Base62String"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Base62String ToBase62String() {
        return Base62String.FromBytes(AsSpan());
    }

    /// <summary>
    /// Returns the uppercase hexadecimal string representation of the hash.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() {
        return Convert.ToHexString(AsSpan());
    }

    /// <summary>
    /// Returns the string representation of the hash using the specified format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(string? format) {
        return ToString(format, null);
    }

    /// <summary>
    /// Returns the string representation of the hash using the specified format and provider.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(string? format, IFormatProvider? formatProvider) {
        return format is "x" ? Convert.ToHexStringLower(AsSpan()) : Convert.ToHexString(AsSpan());
    }

    /// <summary>
    /// Attempts to format the hash as an uppercase hexadecimal string into the destination character span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        return TryFormat(destination, out charsWritten, default, null);
    }

    /// <summary>
    /// Attempts to format the hash into the destination character span using the specified format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format) {
        return TryFormat(destination, out charsWritten, format, null);
    }

    /// <summary>
    /// Attempts to format the hash into the destination character span using the specified format and provider.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
        int required = HashSizeInBytes * 2;
        if(destination.Length < required) { charsWritten = 0; return false; }
        return format.Equals("x", StringComparison.Ordinal)
            ? Convert.TryToHexStringLower(AsSpan(), destination, out charsWritten)
            : Convert.TryToHexString(AsSpan(), destination, out charsWritten);
    }

    /// <summary>
    /// Attempts to format the hash as an uppercase UTF-8 hexadecimal byte span.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        return TryFormat(utf8Destination, out bytesWritten, default, null);
    }

    /// <summary>
    /// Attempts to format the hash into the destination UTF-8 byte span using the specified format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format) {
        return TryFormat(utf8Destination, out bytesWritten, format, null);
    }

    /// <summary>
    /// Attempts to format the hash into the destination UTF-8 byte span using the specified format and provider.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
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

    #endregion

    #region Deconstruction

    /// <summary>
    /// Deconstructs the <see cref="XxHash3"/> struct into its 64-bit unsigned integer hash value.
    /// </summary>
    /// <param name="value">The 64-bit unsigned integer representation of the hash.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Deconstruct(out ulong value) {
        value = this._value;
    }

    #endregion

    #region Equality & Comparison

    /// <summary>
    /// Compares two <see cref="XxHash3"/> hashes for equality using a fast 64-bit integer comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(XxHash3 other) {
        return this._value == other._value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly bool Equals(object? obj) {
        return obj is XxHash3 other && Equals(other);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() {
        return (int)(this._value ^ (this._value >> 32));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(XxHash3 other) {
        return this._value.CompareTo(other._value);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(object? obj) {
        if(obj is null) return 1;
        if(obj is XxHash3 other) return CompareTo(other);
        throw new ArgumentException($"Object must be of type {nameof(XxHash3)}.", nameof(obj));
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(XxHash3 left, XxHash3 right) {
        return left.CompareTo(right) > 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(XxHash3 left, XxHash3 right) {
        return left.CompareTo(right) < 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(XxHash3 left, XxHash3 right) {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc cref="IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(XxHash3 left, XxHash3 right) {
        return left.CompareTo(right) <= 0;
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Equality"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(XxHash3 left, XxHash3 right) {
        return left.Equals(right);
    }

    /// <inheritdoc cref="IEqualityOperators{TSelf,TOther,TResult}.op_Inequality"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(XxHash3 left, XxHash3 right) {
        return !left.Equals(right);
    }

    #endregion

    #region Alternate Comparers (.NET 10 Alternate Lookup)

    /// <summary>
    /// Gets an equality comparer that performs ordinal comparisons on <see cref="XxHash3"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<XxHash3> OrdinalComparer => XxHash3OrdinalComparer.Instance;

    /// <summary>
    /// Gets an equality comparer that performs case-insensitive ordinal comparisons on <see cref="XxHash3"/>
    /// and supports zero-allocation alternate lookups using <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static IEqualityComparer<XxHash3> OrdinalIgnoreCaseComparer => XxHash3OrdinalIgnoreCaseComparer.Instance;

    private sealed class XxHash3OrdinalComparer : IEqualityComparer<XxHash3>, IAlternateEqualityComparer<ReadOnlySpan<char>, XxHash3> {
        public static XxHash3OrdinalComparer Instance { get; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(XxHash3 x, XxHash3 y) {
            return x.Equals(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(XxHash3 obj) {
            return obj.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ReadOnlySpan<char> alternate, XxHash3 other) {
            if(XxHash3.TryParse(alternate, out XxHash3 parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(XxHash3.TryParse(alternate, out XxHash3 parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public XxHash3 Create(ReadOnlySpan<char> alternate) {
            return XxHash3.Parse(alternate);
        }
    }

    private sealed class XxHash3OrdinalIgnoreCaseComparer : IEqualityComparer<XxHash3>, IAlternateEqualityComparer<ReadOnlySpan<char>, XxHash3> {
        public static XxHash3OrdinalIgnoreCaseComparer Instance { get; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(XxHash3 x, XxHash3 y) {
            return x.Equals(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(XxHash3 obj) {
            return obj.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ReadOnlySpan<char> alternate, XxHash3 other) {
            if(XxHash3.TryParse(alternate, out XxHash3 parsed)) {
                return parsed.Equals(other);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(ReadOnlySpan<char> alternate) {
            if(XxHash3.TryParse(alternate, out XxHash3 parsed)) {
                return parsed.GetHashCode();
            }
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public XxHash3 Create(ReadOnlySpan<char> alternate) {
            return XxHash3.Parse(alternate);
        }
    }

    #endregion

    /// <summary>
    /// An <see cref="IBufferWriter{Byte}"/> that feeds written bytes directly into an <see cref="XxHash3Core"/>
    /// hasher, using a pooled backing buffer.
    /// </summary>
    /// <remarks>
    /// Instances are cached per-thread via <see cref="Rent"/>/<see cref="Return"/> so that, in the common
    /// (non-reentrant) case, repeated calls to <see cref="XxHash3.Compute{TState}"/> on the same thread perform
    /// no heap allocations at all after the first warm-up call. The class is intentionally not <see cref="IDisposable"/>
    /// on its own public surface — lifetime is fully owned and managed by <see cref="Rent"/>/<see cref="Return"/>.
    /// </remarks>
    private sealed class StreamingXxHash3Writer : IBufferWriter<byte> {
        private const int DefaultCapacity = 4096;

        // Thread-local single-slot cache: warmed-up Rent/Return pairs allocate nothing.
        [ThreadStatic]
        private static StreamingXxHash3Writer? _cached;

        private readonly XxHash3Core _hasher;
        private byte[] _buffer;
        private int _bufferedBytes;

        private StreamingXxHash3Writer(int capacity) {
            this._hasher = new XxHash3Core();
            this._buffer = ArrayPool<byte>.Shared.Rent(capacity);
        }

        private StreamingXxHash3Writer(int capacity, long seed) {
            this._hasher = new XxHash3Core(seed);
            this._buffer = ArrayPool<byte>.Shared.Rent(capacity);
        }

        /// <summary>
        /// Rents a writer for the current thread. Returns the cached instance (reset) when available,
        /// or allocates a new one on first use or during reentrant calls.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StreamingXxHash3Writer Rent() {
            StreamingXxHash3Writer? writer = _cached;
            if(writer is not null) {
                // Take exclusive ownership for the duration of this call so a reentrant
                // Compute() call (invoked from within writeAction) safely rents a separate instance.
                _cached = null;
                writer._hasher.Reset();
                writer._bufferedBytes = 0;
                return writer;
            }
            return new(DefaultCapacity);
        }

        /// <summary>
        /// Rents a writer for the current thread, initialized with the specified seed. Returns the cached
        /// instance (reseeded) when available, or allocates a new one on first use or during reentrant calls.
        /// </summary>
        /// <param name="seed">The seed to initialize the hash computation with.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StreamingXxHash3Writer Rent(long seed) {
            StreamingXxHash3Writer? writer = _cached;
            if(writer is not null) {
                _cached = null;
                writer._hasher.Reinitialize(seed);
                writer._bufferedBytes = 0;
                return writer;
            }
            return new(DefaultCapacity, seed);
        }

        /// <summary>
        /// Returns the writer to the thread-local cache. If the cache slot was already refilled
        /// (possible after reentrant usage), the writer's buffer is returned to the shared pool instead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(StreamingXxHash3Writer writer) {
            writer.Flush();
            if(_cached is null) {
                _cached = writer;
            }
            else {
                ArrayPool<byte>.Shared.Return(writer._buffer, clearArray: true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count) {
            if((uint)count > (uint)(this._buffer.Length - this._bufferedBytes)) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            this._bufferedBytes += count;
            if(this._bufferedBytes == this._buffer.Length) {
                Flush();
            }
        }

        public Memory<byte> GetMemory(int sizeHint = 0) {
            EnsureCapacity(sizeHint);
            return this._buffer.AsMemory(this._bufferedBytes);
        }

        public Span<byte> GetSpan(int sizeHint = 0) {
            EnsureCapacity(sizeHint);
            return this._buffer.AsSpan(this._bufferedBytes);
        }

        private void EnsureCapacity(int sizeHint) {
            sizeHint = Math.Max(sizeHint, 1);
            if(this._buffer.Length - this._bufferedBytes >= sizeHint) {
                return;
            }

            // Push already-buffered bytes into the hasher to free up space first.
            Flush();

            if(sizeHint > this._buffer.Length) {
                // Rare path: caller wants a single contiguous span bigger than our buffer.
                byte[] old = this._buffer;
                this._buffer = ArrayPool<byte>.Shared.Rent(Math.Max(sizeHint, old.Length * 2));
                ArrayPool<byte>.Shared.Return(old, clearArray: true);
            }
        }

        public void Flush() {
            if(this._bufferedBytes > 0) {
                this._hasher.Append(this._buffer.AsSpan(0, this._bufferedBytes));
                this._bufferedBytes = 0;
            }
        }

        /// <summary>
        /// Flushes any pending buffered bytes and returns the finalized 64-bit hash.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetCurrentHashAsUInt64() {
            Flush();
            return this._hasher.GetCurrentHashAsUInt64();
        }
    }
}

/// <summary>
/// Extension methods for <see cref="XxHash3"/>.
/// </summary>
public static partial class XxHash3Extensions {
    extension(XxHash3) {
        /// <summary>
        /// Asynchronously computes the <see cref="XxHash3"/> hash of a stream using SIMD hardware streaming.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<XxHash3> ComputeAsync(Stream stream) {
            return ComputeAsync(stream, CancellationToken.None);
        }

        /// <summary>
        /// Asynchronously computes the <see cref="XxHash3"/> hash of a stream using SIMD hardware streaming
        /// and the specified seed.
        /// </summary>
        /// <param name="stream">The source stream to hash.</param>
        /// <param name="seed">The seed to initialize the hash computation with.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<XxHash3> ComputeAsync(Stream stream, long seed) {
            return ComputeAsync(stream, CancellationToken.None, seed);
        }

        /// <summary>
        /// Asynchronously computes the <see cref="XxHash3"/> hash of a stream using SIMD hardware streaming.
        /// </summary>
        public static async ValueTask<XxHash3> ComputeAsync(Stream stream, CancellationToken cancellationToken) {
            Preca.ThrowIfNull(stream);
            if(stream.CanSeek) stream.Position = 0;

            XxHash3Core hasher = new();
            byte[] rented = ArrayPool<byte>.Shared.Rent(81_920); // 80 KB chunks

            try {
                int bytesRead;
                while((bytesRead = await stream.ReadAsync(rented, cancellationToken)
                    .ConfigureAwait(false)) > 0) {
                    hasher.Append(rented.AsSpan(0, bytesRead));
                }

                return new(hasher.GetCurrentHashAsUInt64());
            }
            finally {
                ArrayPool<byte>.Shared.Return(rented);
                if(stream.CanSeek) stream.Position = 0;
            }
        }

        /// <summary>
        /// Asynchronously computes the <see cref="XxHash3"/> hash of a stream using SIMD hardware streaming
        /// and the specified seed.
        /// </summary>
        /// <param name="stream">The source stream to hash.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <param name="seed">The seed to initialize the hash computation with.</param>
        public static async ValueTask<XxHash3> ComputeAsync(Stream stream, CancellationToken cancellationToken, long seed) {
            Preca.ThrowIfNull(stream);
            if(stream.CanSeek) stream.Position = 0;

            XxHash3Core hasher = new(seed);
            byte[] rented = ArrayPool<byte>.Shared.Rent(81_920); // 80 KB chunks

            try {
                int bytesRead;
                while((bytesRead = await stream.ReadAsync(rented, cancellationToken)
                    .ConfigureAwait(false)) > 0) {
                    hasher.Append(rented.AsSpan(0, bytesRead));
                }

                return new(hasher.GetCurrentHashAsUInt64());
            }
            finally {
                ArrayPool<byte>.Shared.Return(rented);
                if(stream.CanSeek) stream.Position = 0;
            }
        }
    }
}