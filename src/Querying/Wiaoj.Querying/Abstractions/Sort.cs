using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Querying.JsonConverters;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying;

/// <summary>
/// Represents an immutable sequence of sort criteria for a query request.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(SortJsonConverter))]
public readonly record struct Sort :
    IEquatable<Sort>,
    IReadOnlyList<SortNode>,
    ISpanParsable<Sort>,
    IUtf8SpanParsable<Sort>,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IFormattable,
    IEqualityOperators<Sort, Sort, bool> {

    private const int StackallocCharThreshold = 256;
    private const int MaxUtf8Length = 4096;

    private readonly IReadOnlyList<SortNode>? _nodes;

    /// <summary>
    /// Represents an empty or uninitialized <see cref="Sort"/> instance.
    /// </summary>
    public static readonly Sort Empty = default;

    /// <summary>
    /// Gets the collection of parsed sort nodes.
    /// </summary>
    public IReadOnlyList<SortNode> Nodes => this._nodes ?? [];

    /// <summary>
    /// Gets a value indicating whether this sort instance contains no criteria.
    /// </summary>
    public bool IsEmpty => this.Nodes.Count == 0;

    /// <summary>
    /// Gets the number of sort criteria nodes in the sequence.
    /// </summary>
    public int Count => this.Nodes.Count;

    /// <summary>
    /// Gets the sort node at the specified zero-based index.
    /// </summary>
    /// <param name="index">The zero-based index of the node to get.</param>
    public SortNode this[int index] => this.Nodes[index];

    /// <summary>
    /// Initializes a new instance of the <see cref="Sort"/> struct.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sort() : this((IReadOnlyList<SortNode>?)null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sort"/> struct by parsing a sort expression string.
    /// </summary>
    /// <param name="sortExpression">The sort expression to parse (e.g. <c>-price,createdAt</c>).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sort(string? sortExpression) {
        if(TryParse(sortExpression, out Sort result)) {
            this = result;
        }
        else {
            this = Empty;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sort"/> struct by parsing a sort expression character span.
    /// </summary>
    /// <param name="sortExpression">The sort expression span to parse.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sort(ReadOnlySpan<char> sortExpression) {
        if(TryParse(sortExpression, out Sort result)) {
            this = result;
        }
        else {
            this = Empty;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sort"/> struct with a collection of sort nodes.
    /// </summary>
    /// <param name="nodes">The collection of sort nodes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sort(IReadOnlyList<SortNode>? nodes) {
        this._nodes = nodes ?? [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Sort"/> struct with an array of sort nodes.
    /// </summary>
    /// <param name="nodes">The array of sort nodes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Sort(params SortNode[] nodes) {
        this._nodes = nodes ?? [];
    }

    #region Parsing (Public API)

    /// <summary>
    /// Parses a sort expression string into a <see cref="Sort"/> instance.
    /// </summary>
    /// <param name="s">The sort expression string to parse.</param>
    /// <returns>A parsed <see cref="Sort"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">Thrown when the sort expression format is invalid.</exception>
    public static Sort Parse(string s) {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a sort expression character span into a <see cref="Sort"/> instance.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <returns>A parsed <see cref="Sort"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the input format is invalid.</exception>
    public static Sort Parse(ReadOnlySpan<char> s) {
        if(TryParse(s, out Sort result)) {
            return result;
        }

        throw new FormatException("Invalid sort expression format.");
    }

    /// <summary>
    /// Parses a UTF-8 byte span into a <see cref="Sort"/> instance.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <returns>A parsed <see cref="Sort"/> instance.</returns>
    /// <exception cref="FormatException">Thrown when the input contains an invalid UTF-8 sequence.</exception>
    public static Sort Parse(ReadOnlySpan<byte> utf8Text) {
        if(TryParse(utf8Text, out Sort result)) {
            return result;
        }

        throw new FormatException("Invalid UTF-8 byte sequence for Sort.");
    }

    /// <summary>
    /// Attempts to parse a sort expression string into a <see cref="Sort"/> instance.
    /// </summary>
    /// <param name="s">The sort expression string to parse.</param>
    /// <param name="result">When this method returns, contains the parsed instance if successful; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, out Sort result) {
        if(s is null) {
            result = Empty;
            return true;
        }

        return TryParse(s.AsSpan(), out result);
    }

    /// <summary>
    /// Attempts to parse a sort expression character span into a <see cref="Sort"/> instance.
    /// </summary>
    /// <param name="s">The character span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed instance if successful; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, out Sort result) {
        ReadOnlySpan<char> trimmed = s.Trim();
        if(trimmed.IsEmpty) {
            result = Empty;
            return true;
        }

        List<SortNode>? nodes = null;

        while(!trimmed.IsEmpty) {
            int delimiterIndex = trimmed.IndexOf(QuerySyntax.Comma);
            ReadOnlySpan<char> segment = delimiterIndex >= 0 ? trimmed[..delimiterIndex].Trim() : trimmed;

            if(!segment.IsEmpty) {
                SortDirection direction = SortDirection.Ascending;
                ReadOnlySpan<char> fieldSpan = segment;

                if(segment.StartsWith(QuerySyntax.SortDescendingPrefix)) {
                    direction = SortDirection.Descending;
                    fieldSpan = segment[1..].Trim();
                }
                else if(segment.StartsWith(QuerySyntax.SortAscendingPrefix)) {
                    direction = SortDirection.Ascending;
                    fieldSpan = segment[1..].Trim();
                }

                if(!fieldSpan.IsEmpty) {
                    nodes ??= [];
                    nodes.Add(new SortNode(fieldSpan.ToString(), direction));
                }
            }

            if(delimiterIndex < 0) {
                break;
            }

            trimmed = trimmed[(delimiterIndex + 1)..].Trim();
        }

        result = new Sort(nodes);
        return true;
    }

    /// <summary>
    /// Attempts to parse a UTF-8 byte span into a <see cref="Sort"/> instance.
    /// </summary>
    /// <param name="utf8Text">The UTF-8 byte span to parse.</param>
    /// <param name="result">When this method returns, contains the parsed instance if successful; otherwise, <see cref="Empty"/>.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Sort result) {
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

    #region Enumeration (Struct Enumerator)

    /// <summary>
    /// Returns an enumerator that iterates through the sort nodes.
    /// </summary>
    public Enumerator GetEnumerator() {
        return new(this.Nodes);
    }

    /// <summary>
    /// Provides an allocation-free enumerator over sort nodes.
    /// </summary>
    public struct Enumerator {
        private readonly IReadOnlyList<SortNode> _nodes;
        private int _index;

        internal Enumerator(IReadOnlyList<SortNode> nodes) {
            this._nodes = nodes;
            this._index = -1;
        }

        /// <summary>
        /// Gets the element at the current position of the enumerator.
        /// </summary>
        public readonly SortNode Current => this._nodes[this._index];

        /// <summary>
        /// Advances the enumerator to the next element of the collection.
        /// </summary>
        public bool MoveNext() {
            int nextIndex = this._index + 1;
            if(nextIndex < this._nodes.Count) {
                this._index = nextIndex;
                return true;
            }
            return false;
        }
    }

    IEnumerator<SortNode> IEnumerable<SortNode>.GetEnumerator() {
        return this.Nodes.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return this.Nodes.GetEnumerator();
    }

    #endregion

    #region Equality

    /// <inheritdoc/>
    public bool Equals(Sort other) {
        if(this.Count != other.Count) {
            return false;
        }

        for(int i = 0; i < this.Count; i++) {
            if(!this[i].Equals(other[i])) {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode() {
        HashCode hash = default;
        for(int i = 0; i < this.Count; i++) {
            hash.Add(this[i]);
        }
        return hash.ToHashCode();
    }

    #endregion

    #region Formatting (Public API)

    /// <inheritdoc/>
    public override string ToString() {
        if(this.IsEmpty) {
            return string.Empty;
        }

        int estimatedLength = this.Count * 16;
        using ValueBuffer<char> buffer = new(estimatedLength, stackalloc char[128]);
        Span<char> span = buffer.Span;

        if(TryFormat(span, out int charsWritten)) {
            return span[..charsWritten].ToString();
        }

        return string.Empty;
    }

    /// <summary>
    /// Formats the sort expression into the destination character span.
    /// </summary>
    /// <param name="destination">The destination character span.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten) {
        charsWritten = 0;
        if(this.IsEmpty) {
            return true;
        }

        int offset = 0;
        for(int i = 0; i < this.Count; i++) {
            if(i > 0) {
                if(offset >= destination.Length) {
                    charsWritten = 0;
                    return false;
                }
                destination[offset++] = QuerySyntax.Comma;
            }

            SortNode node = this[i];
            if(node.IsDescending) {
                if(offset >= destination.Length) {
                    charsWritten = 0;
                    return false;
                }
                destination[offset++] = QuerySyntax.SortDescendingPrefix;
            }

            ReadOnlySpan<char> fieldSpan = node.Field.AsSpan();
            if(destination.Length - offset < fieldSpan.Length) {
                charsWritten = 0;
                return false;
            }

            fieldSpan.CopyTo(destination[offset..]);
            offset += fieldSpan.Length;
        }

        charsWritten = offset;
        return true;
    }

    /// <summary>
    /// Formats the sort expression into the destination UTF-8 byte span.
    /// </summary>
    /// <param name="utf8Destination">The destination UTF-8 byte span.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written.</param>
    /// <returns><see langword="true"/> if formatting succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) {
        bytesWritten = 0;
        if(this.IsEmpty) {
            return true;
        }

        Span<char> charBuf = stackalloc char[256];
        if(!TryFormat(charBuf, out int charsWritten)) {
            return false;
        }

        return Utf8.FromUtf16(charBuf[..charsWritten], utf8Destination, out _, out bytesWritten) == OperationStatus.Done;
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

    static Sort IParsable<Sort>.Parse(string s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool IParsable<Sort>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Sort result) {
        return TryParse(s, out result);
    }

    static Sort ISpanParsable<Sort>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) {
        return Parse(s);
    }

    static bool ISpanParsable<Sort>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Sort result) {
        return TryParse(s, out result);
    }

    static Sort IUtf8SpanParsable<Sort>.Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider) {
        return Parse(utf8Text);
    }

    static bool IUtf8SpanParsable<Sort>.TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Sort result) {
        return TryParse(utf8Text, out result);
    }

    #endregion
}