using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Primitives.Hashing;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying;

/// <summary>
/// Represents a query request containing a search term, filter conditions, sort criteria, and query fingerprint.
/// </summary>
[DebuggerDisplay("Q: {Q.Value}, Sort: {Sort}, Filters: {Filters.Count}, Hash: {QueryHash}")]
[StructLayout(LayoutKind.Auto)]
public readonly record struct QueryRequest :
    IEquatable<QueryRequest>,
    ISpanParsable<QueryRequest>,
    IUtf8SpanParsable<QueryRequest>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IEqualityOperators<QueryRequest, QueryRequest, bool> {

    private const int StackallocCharThreshold = 256;
    private const int MaxUtf8Length = 4096;

    /// <summary>
    /// Represents an empty or uninitialized <see cref="QueryRequest"/> instance.
    /// </summary>
    public static readonly QueryRequest Empty = default;

    /// <summary>
    /// Gets the normalized free-text search term.
    /// </summary>
    public Q Q { get; init; }

    /// <summary>
    /// Gets the structured sort criteria sequence.
    /// </summary>
    public Sort Sort { get; init; }

    /// <summary>
    /// Gets the collection of parsed filter condition nodes. Never <see langword="null"/>,
    /// even for <see langword="default"/>-initialized instances that bypass the constructor.
    /// </summary>
    public IReadOnlyList<FilterConditionNode> Filters {
        get => field ?? [];
        init;
    }

    /// <summary>
    /// Gets the fingerprint hash of the query, for caching and ETag generation.
    /// </summary>
    /// <remarks>
    /// Recomputed on every access from the current <see cref="Q"/>, <see cref="Sort"/>, and
    /// <see cref="Filters"/>, so it always reflects the current state — including after a
    /// <c>with</c> expression changes one of those members.
    /// </remarks>
    public XxHash3 QueryHash => ComputeQueryHash(this.Q, this.Sort, this.Filters);

    /// <summary>
    /// Gets a value indicating whether this request instance represents an empty state.
    /// </summary>
    public bool IsEmpty =>
        this.Q.IsEmpty &&
        this.Sort.IsEmpty &&
        this.Filters.Count == 0;
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRequest"/> struct with default values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryRequest() : this(default, default, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRequest"/> struct with a search term.
    /// </summary>
    /// <param name="q">The free-text search term.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryRequest(Q q) : this(q, default, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRequest"/> struct with a sort instance.
    /// </summary>
    /// <param name="sort">The structured sort criteria.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryRequest(Sort sort) : this(default, sort, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRequest"/> struct with filter conditions.
    /// </summary>
    /// <param name="filters">The collection of filter condition nodes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryRequest(IReadOnlyList<FilterConditionNode>? filters) : this(default, default, filters) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRequest"/> struct with a search term and sort instance.
    /// </summary>
    /// <param name="q">The free-text search term.</param>
    /// <param name="sort">The structured sort criteria.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryRequest(Q q, Sort sort) : this(q, sort, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRequest"/> struct with a search term and filter conditions.
    /// </summary>
    /// <param name="q">The free-text search term.</param>
    /// <param name="filters">The collection of filter condition nodes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryRequest(Q q, IReadOnlyList<FilterConditionNode>? filters) : this(q, default, filters) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRequest"/> struct with a sort instance and filter conditions.
    /// </summary>
    /// <param name="sort">The structured sort criteria.</param>
    /// <param name="filters">The collection of filter condition nodes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryRequest(Sort sort, IReadOnlyList<FilterConditionNode>? filters) : this(default, sort, filters) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRequest"/> struct with a search term, sort instance, and filter conditions.
    /// </summary>
    /// <param name="q">The free-text search term.</param>
    /// <param name="sort">The structured sort criteria.</param>
    /// <param name="filters">The collection of filter condition nodes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryRequest(
        Q q,
        Sort sort,
        IReadOnlyList<FilterConditionNode>? filters) {
        this.Q = q;
        this.Sort = sort;
        this.Filters = filters ?? [];
    }

    #region Parsing (Public API)

    /// <summary>
    /// Parses a complete query string into a <see cref="QueryRequest"/> instance.
    /// </summary>
    /// <param name="s">The raw query string to parse.</param>
    /// <returns>A parsed <see cref="QueryRequest"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when the query string format is invalid.</exception>
    public static QueryRequest Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span into a <see cref="QueryRequest"/> instance.
    /// </summary>
    /// <param name="s">The character span containing query parameters to parse.</param>
    /// <returns>A parsed <see cref="QueryRequest"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the input format is invalid.</exception>
    public static QueryRequest Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out QueryRequest result)) {
            return result;
        }

        throw new FormatException("Invalid query string format for QueryRequest.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="QueryRequest"/> instance.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span containing query parameters to parse.</param>
    /// <returns>A parsed <see cref="QueryRequest"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the input contains an invalid UTF-8 sequence or format.</exception>
    public static QueryRequest Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out QueryRequest result)) {
            return result;
        }

        throw new FormatException("Invalid UTF-8 byte sequence for QueryRequest.");
    }

    /// <summary>
    /// Attempts to parse a query string into a <see cref="QueryRequest"/> instance.
    /// </summary>
    /// <param name="s">The query string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed instance if successful; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out QueryRequest result) {
        if(s is null) {
            result = Empty;
            return true;
        }

        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a query character span into a <see cref="QueryRequest"/> instance.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed instance if successful; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out QueryRequest result) {
        ReadOnlySpan<char> trimmed = s.Trim();
        if(trimmed.IsEmpty) {
            result = Empty;
            return true;
        }

        if(trimmed.StartsWith(QuerySyntax.QueryStart)) {
            trimmed = trimmed[1..].Trim();
            if(trimmed.IsEmpty) {
                result = Empty;
                return true;
            }
        }

        Q q = default;
        Sort sort = default;
        List<FilterConditionNode>? filters = null;

        while(!trimmed.IsEmpty) {
            int delimiterIndex = trimmed.IndexOf(QuerySyntax.ParameterDelimiter);
            ReadOnlySpan<char> segment = delimiterIndex >= 0 ? trimmed[..delimiterIndex].Trim() : trimmed;

            if(!segment.IsEmpty) {
                if(segment.StartsWith(QuerySyntax.Parameters.QPrefix, StringComparison.OrdinalIgnoreCase)) {
                    q = new Q(segment[QuerySyntax.Parameters.QPrefix.Length..].Trim());
                }
                else if(segment.StartsWith(QuerySyntax.Parameters.SortPrefix, StringComparison.OrdinalIgnoreCase)) {
                    if(Sort.TryParse(segment[QuerySyntax.Parameters.SortPrefix.Length..].Trim(), out Sort parsedSort)) {
                        sort = parsedSort;
                    }
                }
                else if(BracketQueryParser.TryParse(segment, out FilterConditionNode filterNode)) {
                    filters ??= [];
                    filters.Add(filterNode);
                }
            }

            if(delimiterIndex < 0) {
                break;
            }

            trimmed = trimmed[(delimiterIndex + 1)..].Trim();
        }

        result = new QueryRequest(q: q, sort: sort, filters: filters);
        return true;
    }

    /// <summary>
    /// Attempts to parse a UTF-8 byte span into a <see cref="QueryRequest"/> instance.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed instance if successful; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out QueryRequest result) {
        if(utf8Text.IsEmpty) {
            result = Empty;
            return true;
        }

        if(utf8Text.Length > MaxUtf8Length) {
            result = Empty;
            return false;
        }

        using ValueBuffer<char> buffer = new(utf8Text.Length, stackalloc char[StackallocCharThreshold]);
        Span<char> charBuffer = buffer.Span;

        if(Utf8.ToUtf16(utf8Text, charBuffer, out _, out int charsWritten, replaceInvalidSequences: false) == OperationStatus.Done) {
            return TryParse(charBuffer[..charsWritten], out result);
        }

        result = Empty;
        return false;
    }

    #endregion

    #region Equality (structural, consistent with QueryHash)

    /// <summary>
    /// Determines whether this instance equals <paramref name="other"/> by comparing
    /// <see cref="Q"/>, <see cref="Sort"/>, and the contents of <see cref="Filters"/> element by element.
    /// </summary>
    /// <remarks>
    /// Overrides the compiler-synthesized <c>record struct</c> equality, which would otherwise
    /// compare <see cref="Filters"/> by reference and treat a <see langword="default"/>-initialized
    /// instance as unequal to an explicitly empty one, even though both have the same
    /// <see cref="QueryHash"/> and are both <see cref="IsEmpty"/>.
    /// </remarks>
    /// <param name="other">The other instance to compare against.</param>
    /// <returns><see langword="true"/> if both instances are structurally equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(QueryRequest other) {
        if(!this.Q.Equals(other.Q)) {
            return false;
        }

        if(!this.Sort.Equals(other.Sort)) {
            return false;
        }

        return FiltersEqual(this.Filters, other.Filters);
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        HashCode hash = default;
        hash.Add(this.Q);
        hash.Add(this.Sort);
        for(int i = 0; i < this.Filters.Count; i++) {
            hash.Add(this.Filters[i]);
        }

        return hash.ToHashCode();
    }

    private static bool FiltersEqual(IReadOnlyList<FilterConditionNode> left, IReadOnlyList<FilterConditionNode> right) {
        if(left.Count != right.Count) {
            return false;
        }

        for(int i = 0; i < left.Count; i++) {
            if(!left[i].Equals(right[i])) {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Query Hash Computation

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static XxHash3 ComputeQueryHash(Q q, Sort sort, IReadOnlyList<FilterConditionNode> filters) {
        if(q.IsEmpty && sort.IsEmpty && filters.Count == 0) {
            return XxHash3.Empty;
        }

        int estimatedLength = (q.Length + (sort.Count * 16)) * 3;
        for(int i = 0; i < filters.Count; i++) {
            estimatedLength += (filters[i].Field.Length + (filters[i].RawValue?.Length ?? 0)) * 3 + 1;
        }

        using ValueBuffer<byte> buffer = new(estimatedLength, stackalloc byte[512]);
        Span<byte> span = buffer.Span;
        int offset = 0;

        if(!q.IsEmpty) {
            if(Utf8.FromUtf16(q.AsSpan(), span[offset..], out _, out int bytesWritten) == OperationStatus.Done) {
                offset += bytesWritten;
            }
        }

        if(!sort.IsEmpty) {
            for(int i = 0; i < sort.Count; i++) {
                SortNode node = sort[i];
                span[offset++] = (byte)(node.IsDescending ? '-' : '+');
                if(Utf8.FromUtf16(node.Field.AsSpan(), span[offset..], out _, out int sortBytes) == OperationStatus.Done) {
                    offset += sortBytes;
                }
            }
        }

        for(int i = 0; i < filters.Count; i++) {
            FilterConditionNode filter = filters[i];

            if(Utf8.FromUtf16(filter.Field.AsSpan(), span[offset..], out _, out int fieldBytes) == OperationStatus.Done) {
                offset += fieldBytes;
            }

            span[offset++] = (byte)filter.Operator;

            if(!string.IsNullOrEmpty(filter.RawValue)) {
                if(Utf8.FromUtf16(filter.RawValue.AsSpan(), span[offset..], out _, out int valBytes) == OperationStatus.Done) {
                    offset += valBytes;
                }
            }
        }

        return XxHash3.Compute(span[..offset]);
    }

    #endregion

    #region Formatting (Public API)

    /// <inheritdoc/>
    public override string ToString() {
        if(this.IsEmpty) {
            return "[Empty QueryRequest]";
        }

        string sortStr = this.Sort.IsEmpty ? "[None]" : this.Sort.ToString();
        return $"Q: {this.Q.Value}, Sort: {sortStr}, Filters: {this.Filters.Count}";
    }

    /// <summary>
    /// Formats the query request into the destination character span.
    /// </summary>
    /// <param name="destination">The span of characters to write to.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        if(this.IsEmpty) {
            return destination.TryWrite(CultureInfo.InvariantCulture, $"[Empty QueryRequest]", out charsWritten);
        }

        string sortStr = this.Sort.IsEmpty ? "[None]" : this.Sort.ToString();
        return destination.TryWrite(
            CultureInfo.InvariantCulture,
            $"Q: {this.Q.Value}, Sort: {sortStr}, Filters: {this.Filters.Count}",
            out charsWritten);
    }

    /// <summary>
    /// Formats the query request into the destination UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The span of UTF-8 bytes to write to.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        if(this.IsEmpty) {
            return Utf8.TryWrite(utf8Destination, CultureInfo.InvariantCulture, $"[Empty QueryRequest]", out bytesWritten);
        }

        string sortStr = this.Sort.IsEmpty ? "[None]" : this.Sort.ToString();
        return Utf8.TryWrite(
            utf8Destination,
            CultureInfo.InvariantCulture,
            $"Q: {this.Q.Value}, Sort: {sortStr}, Filters: {this.Filters.Count}",
            out bytesWritten);
    }

    #endregion

    #region Explicit Interface Implementations

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

    static QueryRequest IParsable<QueryRequest>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<QueryRequest>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out QueryRequest result) {
        return TryParse(s, out result);
    }

    static QueryRequest ISpanParsable<QueryRequest>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<QueryRequest>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out QueryRequest result) {
        return TryParse(s, out result);
    }

    static QueryRequest IUtf8SpanParsable<QueryRequest>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<QueryRequest>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out QueryRequest result) {
        return TryParse(utf8Text, out result);
    }

    #endregion
}