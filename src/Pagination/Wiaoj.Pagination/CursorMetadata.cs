using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Wiaoj.Pagination.JsonConverters;

namespace Wiaoj.Pagination;

/// <summary>
/// Represents immutable, zero-allocation metadata for keyset (cursor-based) pagination.
/// </summary>
/// <remarks>
/// Aligned with the global cursor specification (e.g. GraphQL Relay spec), providing forward and backward cursor boundaries.
/// </remarks>
[DebuggerDisplay("Start: {StartCursor.Value}, End: {EndCursor.Value}, HasNext: {HasNext}")]
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(CursorMetadataJsonConverter))]
public readonly record struct CursorMetadata :
    IEquatable<CursorMetadata>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IEqualityOperators<CursorMetadata, CursorMetadata, bool> {

    /// <summary>
    /// Represents an empty or uninitialized <see cref="CursorMetadata"/> instance.
    /// </summary>
    public static readonly CursorMetadata Empty = default;

    /// <summary>
    /// Gets the cursor token pointing to the first item in the current window.
    /// </summary>
    public CursorToken StartCursor { get; }

    /// <summary>
    /// Gets the cursor token pointing to the last item in the current window.
    /// </summary>
    public CursorToken EndCursor { get; }

    /// <summary>
    /// Gets a value indicating whether there are preceding records before the current window.
    /// </summary>
    public bool HasPrevious { get; }

    /// <summary>
    /// Gets a value indicating whether there are succeeding records after the current window.
    /// </summary>
    public bool HasNext { get; }

    /// <summary>
    /// Gets a value indicating whether this metadata instance represents an empty or uninitialized state.
    /// </summary>
    public bool IsEmpty => this.StartCursor.IsEmpty && this.EndCursor.IsEmpty && !this.HasPrevious && !this.HasNext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CursorMetadata"/> struct.
    /// </summary>
    /// <param name="startCursor">The cursor token of the first item.</param>
    /// <param name="endCursor">The cursor token of the last item.</param>
    /// <param name="hasPrevious">Whether a previous page exists.</param>
    /// <param name="hasNext">Whether a next page exists.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CursorMetadata(CursorToken startCursor, CursorToken endCursor, bool hasPrevious, bool hasNext) {
        this.StartCursor = startCursor;
        this.EndCursor = endCursor;
        this.HasPrevious = hasPrevious;
        this.HasNext = hasNext;
    }

    /// <summary>
    /// Deconstructs the <see cref="CursorMetadata"/> into its boundary components.
    /// </summary>
    /// <param name="startCursor">The starting cursor token.</param>
    /// <param name="endCursor">The ending cursor token.</param>
    /// <param name="hasPrevious">The previous page existence flag.</param>
    /// <param name="hasNext">The next page existence flag.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out CursorToken startCursor, out CursorToken endCursor, out bool hasPrevious, out bool hasNext) {
        startCursor = this.StartCursor;
        endCursor = this.EndCursor;
        hasPrevious = this.HasPrevious;
        hasNext = this.HasNext;
    }

    #region Formatting (ISpanFormattable, IUtf8SpanFormattable, IFormattable)

    /// <inheritdoc/>
    public override string ToString() {
        if(IsEmpty) {
            return "Start: [None], End: [None], HasPrevious: False, HasNext: False";
        }
        return $"Start: {this.StartCursor.Value}, End: {this.EndCursor.Value}, HasPrevious: {this.HasPrevious}, HasNext: {this.HasNext}";
    }

    /// <summary>
    /// Tries to format the cursor metadata into the destination character span with zero allocations.
    /// </summary>
    /// <param name="destination">The destination character buffer.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(IsEmpty) {
            return destination.TryWrite(CultureInfo.InvariantCulture, $"Start: [None], End: [None], HasPrevious: False, HasNext: False", out charsWritten);
        }

        return destination.TryWrite(
            CultureInfo.InvariantCulture,
            $"Start: {this.StartCursor.Value}, End: {this.EndCursor.Value}, HasPrevious: {this.HasPrevious}, HasNext: {this.HasNext}",
            out charsWritten);
    }

    /// <summary>
    /// Tries to format the cursor metadata into the destination UTF-8 byte span with zero allocations.
    /// </summary>
    /// <param name="utf8Destination">The destination UTF-8 byte buffer.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        if(IsEmpty) {
            return System.Text.Unicode.Utf8.TryWrite(utf8Destination, CultureInfo.InvariantCulture, $"Start: [None], End: [None], HasPrevious: False, HasNext: False", out bytesWritten);
        }

        return System.Text.Unicode.Utf8.TryWrite(
            utf8Destination,
            CultureInfo.InvariantCulture,
            $"Start: {this.StartCursor.Value}, End: {this.EndCursor.Value}, HasPrevious: {this.HasPrevious}, HasNext: {this.HasNext}",
            out bytesWritten);
    }

    // --- Explicit Interface Implementations ---

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => ToString();

    bool ISpanFormattable.TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) => TryFormat(destination, out charsWritten);

    bool IUtf8SpanFormattable.TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider) => TryFormat(utf8Destination, out bytesWritten);

    #endregion
}