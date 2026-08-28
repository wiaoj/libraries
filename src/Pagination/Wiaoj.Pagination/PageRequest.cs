using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wiaoj.Preconditions;

namespace Wiaoj.Pagination;

/// <summary>
/// Represents an immutable, zero-allocation request for offset-based pagination.
/// </summary>
/// <remarks>
/// <para>
/// Implements <see cref="ISpanParsable{TSelf}"/> to enable zero-allocation HTTP parameter binding
/// in ASP.NET Core Minimal APIs and query string parsers without framework dependencies.
/// </para>
/// </remarks>
[DebuggerDisplay("PageNumber = {PageNumber}, PageSize = {PageSize}")]
[StructLayout(LayoutKind.Auto)]
public readonly record struct PageRequest :
    IEquatable<PageRequest>,
    ISpanParsable<PageRequest>,
    IUtf8SpanParsable<PageRequest>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<PageRequest, PageRequest, bool> {

    /// <summary>
    /// The default page size limit applied when not specified.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// The absolute maximum allowed page size to protect database throughput.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Represents an uninitialized <see cref="PageRequest"/> instance.
    /// </summary>
    public static readonly PageRequest Empty = default;

    /// <summary>
    /// Represents the default pagination request (Page 1, Size 20).
    /// </summary>
    public static readonly PageRequest Default = new(1, DefaultPageSize);

    /// <summary>
    /// Gets the 1-based requested page index.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the requested page capacity.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets a value indicating whether this request is uninitialized.
    /// </summary>
    public bool IsEmpty => this.PageNumber == 0 && this.PageSize == 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="PageRequest"/> struct with boundary clamping.
    /// </summary>
    /// <param name="pageNumber">The 1-based page index. Clamped to minimum 1.</param>
    /// <param name="pageSize">The requested page size. Clamped between 1 and <see cref="MaxPageSize"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PageRequest(int pageNumber = 1, int pageSize = DefaultPageSize) {
        this.PageNumber = pageNumber < 1 ? 1 : pageNumber;
        this.PageSize = pageSize < 1 ? DefaultPageSize : (pageSize > MaxPageSize ? MaxPageSize : pageSize);
    }

    /// <summary>
    /// Calculates the number of records to skip in SQL / LINQ queries, protected against integer overflow.
    /// </summary>
    /// <returns>The zero-based offset calculation. Clamped to <see cref="int.MaxValue"/> on overflow.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CalculateSkip() {
        if(this.PageNumber <= 1 || this.PageSize <= 0) {
            return 0;
        }

        long skip = (long)(this.PageNumber - 1) * this.PageSize;
        return skip > int.MaxValue ? int.MaxValue : (int)skip;
    }

    /// <summary>
    /// Deconstructs the request into its page index and size components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out int pageNumber, out int pageSize) {
        pageNumber = this.PageNumber;
        pageSize = this.PageSize;
    }

    #region Parsing (ISpanParsable, IUtf8SpanParsable)

    /// <summary>
    /// Parses a string formatted as <c>PageNumber:PageSize</c> or <c>PageNumber,PageSize</c>.
    /// </summary>
    public static PageRequest Parse(string s) {
        Preca.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span formatted as <c>PageNumber:PageSize</c> into a <see cref="PageRequest"/>.
    /// </summary>
    public static PageRequest Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out PageRequest result)) {
            return result;
        }
        throw new FormatException("Invalid PageRequest format. Expected 'PageNumber:PageSize' or 'PageNumber,PageSize'.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="PageRequest"/>.
    /// </summary>
    public static PageRequest Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out PageRequest result)) {
            return result;
        }
        throw new FormatException("Invalid UTF-8 sequence for PageRequest.");
    }

    /// <summary>
    /// Tries to parse a string into a <see cref="PageRequest"/>.
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out PageRequest result) {
        if(s is null) {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="PageRequest"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out PageRequest result) {
        if(s.IsEmpty) {
            result = Default;
            return true;
        }

        int separatorIndex = s.IndexOfAny(':', ',');
        if(separatorIndex < 0) {
            if(int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pageOnly)) {
                result = new PageRequest(pageOnly, DefaultPageSize);
                return true;
            }
            result = default;
            return false;
        }

        ReadOnlySpan<char> pageSpan = s[..separatorIndex];
        ReadOnlySpan<char> sizeSpan = s[(separatorIndex + 1)..];

        if(int.TryParse(pageSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out int page) &&
           int.TryParse(sizeSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out int size)) {
            result = new PageRequest(page, size);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Tries to parse a UTF-8 byte span into a <see cref="PageRequest"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out PageRequest result) {
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
        if(this.IsEmpty) return "PageNumber: 0, PageSize: 0";
        return $"{this.PageNumber}:{this.PageSize}";
    }

    /// <summary>
    /// Tries to format the page request into the destination character span with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.IsEmpty) {
            return destination.TryWrite(CultureInfo.InvariantCulture, $"PageNumber: 0, PageSize: 0", out charsWritten);
        }
        return destination.TryWrite(CultureInfo.InvariantCulture, $"{this.PageNumber}:{this.PageSize}", out charsWritten);
    }

    /// <summary>
    /// Tries to format the page request into the destination UTF-8 byte span with zero allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        if(this.IsEmpty) {
            return System.Text.Unicode.Utf8.TryWrite(utf8Destination, CultureInfo.InvariantCulture, $"PageNumber: 0, PageSize: 0", out bytesWritten);
        }
        return System.Text.Unicode.Utf8.TryWrite(utf8Destination, CultureInfo.InvariantCulture, $"{this.PageNumber}:{this.PageSize}", out bytesWritten);
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

    static PageRequest IParsable<PageRequest>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<PageRequest>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out PageRequest result) {
        return TryParse(s, out result);
    }

    static PageRequest ISpanParsable<PageRequest>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<PageRequest>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out PageRequest result) {
        return TryParse(s, out result);
    }

    static PageRequest IUtf8SpanParsable<PageRequest>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<PageRequest>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out PageRequest result) {
        return TryParse(utf8Text, out result);
    }

    #endregion
}