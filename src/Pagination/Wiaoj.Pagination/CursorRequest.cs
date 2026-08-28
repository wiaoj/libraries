using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wiaoj.Preconditions;

namespace Wiaoj.Pagination;

/// <summary>
/// Represents an immutable, zero-allocation request for keyset (cursor-based) pagination.
/// </summary>
[DebuggerDisplay("Cursor = {Cursor.Value}, Limit = {Limit}, Direction = {Direction}")]
[StructLayout(LayoutKind.Auto)]
public readonly record struct CursorRequest :
    IEquatable<CursorRequest>,
    ISpanParsable<CursorRequest>,
    IUtf8SpanParsable<CursorRequest>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<CursorRequest, CursorRequest, bool> {

    /// <summary>
    /// The default item limit applied when not specified.
    /// </summary>
    public const int DefaultLimit = 20;

    /// <summary>
    /// The maximum allowed item limit per keyset window.
    /// </summary>
    public const int MaxLimit = 100;

    /// <summary>
    /// Represents an uninitialized <see cref="CursorRequest"/> instance.
    /// </summary>
    public static readonly CursorRequest Empty = default;

    /// <summary>
    /// Represents the default forward keyset request starting from the beginning.
    /// </summary>
    public static readonly CursorRequest Default = new(CursorToken.Empty, DefaultLimit, CursorDirection.Forward);

    /// <summary>
    /// Gets the cursor token indicating the seek position.
    /// </summary>
    public CursorToken Cursor { get; }

    /// <summary>
    /// Gets the maximum number of items to fetch.
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// Gets the keyset traversal direction.
    /// </summary>
    public CursorDirection Direction { get; }

    /// <summary>
    /// Gets a value indicating whether this request is uninitialized.
    /// </summary>
    public bool IsEmpty => this.Cursor.IsEmpty && this.Limit == 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="CursorRequest"/> struct with boundary clamping.
    /// </summary>
    /// <param name="cursor">The cursor token.</param>
    /// <param name="limit">The item limit. Clamped between 1 and <see cref="MaxLimit"/>.</param>
    /// <param name="direction">The traversal direction.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CursorRequest(CursorToken cursor, int limit = DefaultLimit, CursorDirection direction = CursorDirection.Forward) {
        this.Cursor = cursor;
        this.Limit = limit < 1 ? DefaultLimit : (limit > MaxLimit ? MaxLimit : limit);
        this.Direction = direction;
    }

    /// <summary>
    /// Deconstructs the cursor request into its primary components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out CursorToken cursor, out int limit, out CursorDirection direction) {
        cursor = this.Cursor;
        limit = this.Limit;
        direction = this.Direction;
    }

    #region Parsing (ISpanParsable, IUtf8SpanParsable)

    /// <summary>
    /// Parses a string formatted as <c>Cursor:Limit:Direction</c> into a <see cref="CursorRequest"/>.
    /// </summary>
    public static CursorRequest Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span formatted as <c>Cursor:Limit:Direction</c> into a <see cref="CursorRequest"/>.
    /// </summary>
    public static CursorRequest Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out CursorRequest result)) {
            return result;
        }
        throw new FormatException("Invalid CursorRequest format. Expected 'Cursor:Limit' or 'Cursor:Limit:Direction'.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="CursorRequest"/>.
    /// </summary>
    public static CursorRequest Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out CursorRequest result)) {
            return result;
        }
        throw new FormatException("Invalid UTF-8 sequence for CursorRequest.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="CursorRequest"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out CursorRequest result) {
        if(s is null) {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="CursorRequest"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out CursorRequest result) {
        if(s.IsEmpty) {
            result = Default;
            return true;
        }

        int firstColon = s.IndexOf(':');
        if(firstColon < 0) {
            // Only cursor provided
            if(CursorToken.TryParse(s, out CursorToken tokenOnly)) {
                result = new CursorRequest(tokenOnly, DefaultLimit, CursorDirection.Forward);
                return true;
            }
            result = default;
            return false;
        }

        ReadOnlySpan<char> cursorSpan = s[..firstColon];
        ReadOnlySpan<char> remainder = s[(firstColon + 1)..];

        if(!CursorToken.TryParse(cursorSpan, out CursorToken cursor)) {
            result = default;
            return false;
        }

        int secondColon = remainder.IndexOf(':');
        if(secondColon < 0) {
            if(int.TryParse(remainder, NumberStyles.Integer, CultureInfo.InvariantCulture, out int limitOnly)) {
                result = new CursorRequest(cursor, limitOnly, CursorDirection.Forward);
                return true;
            }
            result = default;
            return false;
        }

        ReadOnlySpan<char> limitSpan = remainder[..secondColon];
        ReadOnlySpan<char> directionSpan = remainder[(secondColon + 1)..];

        if(int.TryParse(limitSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit) &&
           Enum.TryParse(directionSpan, ignoreCase: true, out CursorDirection direction)) {
            result = new CursorRequest(cursor, limit, direction);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="CursorRequest"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out CursorRequest result) {
        if(utf8Text.IsEmpty) {
            result = Default;
            return true;
        }

        Span<char> charBuf = stackalloc char[utf8Text.Length];
        if(System.Text.Ascii.ToUtf16(utf8Text, charBuf, out _) == System.Buffers.OperationStatus.Done) {
            return TryParse(charBuf, out result);
        }

        result = default;
        return false;
    }

    #endregion

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <inheritdoc/>
    public override string ToString() {
        if(this.IsEmpty) return "Cursor: [None], Limit: 0, Direction: Forward";
        return $"{this.Cursor.Value}:{this.Limit}:{this.Direction}";
    }

    /// <summary>
    /// Tries to format the cursor request into the destination character span with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.IsEmpty) {
            return destination.TryWrite(CultureInfo.InvariantCulture, $"Cursor: [None], Limit: 0, Direction: Forward", out charsWritten);
        }
        return destination.TryWrite(CultureInfo.InvariantCulture, $"{this.Cursor.Value}:{this.Limit}:{this.Direction}", out charsWritten);
    }

    /// <summary>
    /// Tries to format the cursor request into the destination UTF-8 byte span with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        if(this.IsEmpty) {
            return System.Text.Unicode.Utf8.TryWrite(utf8Destination, CultureInfo.InvariantCulture, $"Cursor: [None], Limit: 0, Direction: Forward", out bytesWritten);
        }
        return System.Text.Unicode.Utf8.TryWrite(utf8Destination, CultureInfo.InvariantCulture, $"{this.Cursor.Value}:{this.Limit}:{this.Direction}", out bytesWritten);
    }

    // --- Explicit Interface Implementations ---

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) {
        return ToString();
    }

    bool ISpanFormattable.TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) {
        return TryFormat(destination, out charsWritten);
    }

    bool IUtf8SpanFormattable.TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) {
        return TryFormat(utf8Destination, out bytesWritten);
    }

    static CursorRequest IParsable<CursorRequest>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<CursorRequest>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CursorRequest result) {
        return TryParse(s, out result);
    }

    static CursorRequest ISpanParsable<CursorRequest>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<CursorRequest>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out CursorRequest result) {
        return TryParse(s, out result);
    }

    static CursorRequest IUtf8SpanParsable<CursorRequest>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<CursorRequest>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out CursorRequest result) {
        return TryParse(utf8Text, out result);
    }

    #endregion
}