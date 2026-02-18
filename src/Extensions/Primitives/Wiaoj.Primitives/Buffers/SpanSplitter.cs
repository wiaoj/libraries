using System.Buffers;
using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives.Buffers;


/// <summary>
/// A high-performance, zero-allocation splitter for <see cref="ReadOnlySpan{char}"/>.
/// It uses the enumerator pattern to allow usage in <c>foreach</c> loops without any heap allocations.
/// </summary>
public ref struct SpanSplitter {
    private ReadOnlySpan<char> _remaining;
    private readonly char _separator;
    private readonly SearchValues<char>? _searchValues;
    private ReadOnlySpan<char> _current;
    private bool _isStarted;

    /// <summary>
    /// Initializes a splitter for a single character separator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanSplitter(ReadOnlySpan<char> span, char separator) {
        this._remaining = span;
        this._separator = separator;
        this._searchValues = null;
        this._current = default;
        this._isStarted = false;
    }

    /// <summary>
    /// Initializes a splitter for multiple character separators using optimized <see cref="SearchValues{char}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanSplitter(ReadOnlySpan<char> span, SearchValues<char> searchValues) {
        this._remaining = span;
        this._searchValues = searchValues;
        this._separator = default;
        this._current = default;
        this._isStarted = false;
    }

    /// <summary>
    /// Returns this instance as an enumerator for use in a <c>foreach</c> loop.
    /// </summary>
    public readonly SpanSplitter GetEnumerator() {
        return this;
    }

    /// <summary>
    /// Gets the element at the current position of the enumerator.
    /// </summary>
    public readonly ReadOnlySpan<char> Current => this._current;

    /// <summary>
    /// Advances the enumerator to the next segment of the span.
    /// </summary>
    /// <returns><see langword="true"/> if the enumerator was successfully advanced; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() {
        if(!this._isStarted) {
            if(this._remaining.IsEmpty) return false;
            this._isStarted = true;
        }

        // Eğer kalan kısım boşsa ama döngü bitmediyse (son segment durumu)
        if(this._remaining.IsEmpty)
            return false;

        // Ayırıcı tipine göre en hızlı arama metodunu seçiyoruz
        int index = this._searchValues != null
            ? this._remaining.IndexOfAny(this._searchValues)
            : this._remaining.IndexOf(this._separator);

        if(index == -1) {
            this._current = this._remaining;
            this._remaining = default;
            return true;
        }

        this._current = this._remaining[..index];
        this._remaining = this._remaining[(index + 1)..];
        return true;
    }
}

public static class SpanSplitterExtensions {
    /// <summary>Splits a string into segments without allocating new strings or arrays.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanSplitter SplitValue(this string? s, char separator) {
        return new(s.AsSpan(), separator);
    }

    /// <summary>Splits a span into segments without any heap allocation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanSplitter SplitValue(this ReadOnlySpan<char> span, char separator) {
        return new(span, separator);
    }

    /// <summary>Splits a span using multiple optimized separators.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanSplitter SplitValue(this ReadOnlySpan<char> span, SearchValues<char> searchValues) {
        return new(span, searchValues);
    }
}