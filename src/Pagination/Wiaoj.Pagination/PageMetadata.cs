using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Wiaoj.Pagination.JsonConverters;

namespace Wiaoj.Pagination;

/// <summary>
/// Represents immutable, zero-allocation metadata for offset-based pagination.
/// </summary>
/// <remarks>
/// <para>
/// This struct implements <see cref="ISpanFormattable"/> and <see cref="IUtf8SpanFormattable"/> to support
/// zero-allocation formatting into HTTP response headers, streaming writers, and logging pipelines.
/// </para>
/// <para>
/// Parameter inputs are defensively sanitized upon construction to prevent division-by-zero or negative state.
/// An uninitialized (<see langword="default"/>) instance represents <see cref="Empty"/> where <see cref="IsEmpty"/> is <see langword="true"/>.
/// </para>
/// </remarks>
[DebuggerDisplay("Page {PageNumber} of {TotalPages} (Total: {TotalCount})")]
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(PageMetadataJsonConverter))]
public readonly record struct PageMetadata :
    IEquatable<PageMetadata>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IEqualityOperators<PageMetadata, PageMetadata, bool> {

    // -------------------------------------------------------------------------
    // CONSTANTS & FACTORIES
    // -------------------------------------------------------------------------

    /// <summary>
    /// Represents an empty or uninitialized <see cref="PageMetadata"/> instance (default struct state).
    /// </summary>
    public static readonly PageMetadata Empty = default;

    // -------------------------------------------------------------------------
    // PROPERTIES
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets the total number of items across all pages.
    /// </summary>
    public long TotalCount { get; }

    /// <summary>
    /// Gets the current 1-based page index.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the maximum number of items per page.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets a value indicating whether this metadata instance is uninitialized or empty.
    /// </summary>
    public bool IsEmpty => this.PageSize == 0;

    /// <summary>
    /// Gets the total number of pages calculated from <see cref="TotalCount"/> and <see cref="PageSize"/>.
    /// Returns 0 if <see cref="PageSize"/> is 0.
    /// </summary>
    /// <remarks>
    /// Uses a division/remainder based ceiling calculation rather than <c>(TotalCount + PageSize - 1) / PageSize</c>
    /// to avoid <see cref="long"/> overflow when <see cref="TotalCount"/> is near <see cref="long.MaxValue"/>.
    /// </remarks>
    public long TotalPages => this.PageSize > 0
        ? (this.TotalCount / this.PageSize) + (this.TotalCount % this.PageSize == 0 ? 0 : 1)
        : 0;

    /// <summary>
    /// Gets a value indicating whether there is a preceding page available.
    /// </summary>
    public bool HasPrevious => this.PageNumber > 1 && this.TotalCount > 0;

    /// <summary>
    /// Gets a value indicating whether there is a succeeding page available.
    /// </summary>
    public bool HasNext => this.PageNumber < this.TotalPages;

    // -------------------------------------------------------------------------
    // CONSTRUCTOR & DECONSTRUCTOR
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initializes a new instance of the <see cref="PageMetadata"/> struct with defensive bound sanitization.
    /// </summary>
    /// <param name="totalCount">The total number of records across all pages. Negative values are clamped to 0.</param>
    /// <param name="pageNumber">The 1-based page number. Values less than 1 are clamped to 1.</param>
    /// <param name="pageSize">The page size limit. Values less than 1 are clamped to 1.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PageMetadata(long totalCount, int pageNumber, int pageSize) {
        this.TotalCount = totalCount < 0 ? 0 : totalCount;
        this.PageNumber = pageNumber < 1 ? 1 : pageNumber;
        this.PageSize = pageSize < 1 ? 1 : pageSize;
    }

    /// <summary>
    /// Deconstructs the <see cref="PageMetadata"/> into its primary components.
    /// </summary>
    /// <param name="totalCount">The total item count.</param>
    /// <param name="pageNumber">The current page index.</param>
    /// <param name="pageSize">The page size capacity.</param>
    /// <param name="totalPages">The calculated total page count.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out long totalCount, out int pageNumber, out int pageSize, out long totalPages) {
        totalCount = this.TotalCount;
        pageNumber = this.PageNumber;
        pageSize = this.PageSize;
        totalPages = this.TotalPages;
    }

    // -------------------------------------------------------------------------
    // FORMATTING (CLEAN PUBLIC API)
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public override string ToString() {
        if(this.IsEmpty) {
            return "Page 0 of 0 (Total: 0)";
        }
        return $"Page {this.PageNumber} of {this.TotalPages} (Total: {this.TotalCount})";
    }

    /// <summary>
    /// Tries to format the metadata into the destination character span with zero allocations.
    /// </summary>
    /// <param name="destination">The destination character buffer.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.IsEmpty) {
            return destination.TryWrite(CultureInfo.InvariantCulture, $"Page 0 of 0 (Total: 0)", out charsWritten);
        }

        return destination.TryWrite(
            CultureInfo.InvariantCulture,
            $"Page {this.PageNumber} of {this.TotalPages} (Total: {this.TotalCount})",
            out charsWritten);
    }

    /// <summary>
    /// Tries to format the metadata into the destination UTF-8 byte span with zero allocations.
    /// </summary>
    /// <param name="utf8Destination">The destination UTF-8 byte buffer.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        if(this.IsEmpty) {
            return System.Text.Unicode.Utf8.TryWrite(utf8Destination, CultureInfo.InvariantCulture, $"Page 0 of 0 (Total: 0)", out bytesWritten);
        }

        return System.Text.Unicode.Utf8.TryWrite(
            utf8Destination,
            CultureInfo.InvariantCulture,
            $"Page {this.PageNumber} of {this.TotalPages} (Total: {this.TotalCount})",
            out bytesWritten);
    }

    // -------------------------------------------------------------------------
    // EXPLICIT INTERFACE IMPLEMENTATIONS (RUNTIME BCL INTEGRATION)
    // -------------------------------------------------------------------------

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
}