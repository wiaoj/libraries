using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives.Buffers;


/// <summary>
/// A high-performance, zero-allocation splitter for <see cref="ReadOnlySpan{char}"/>.
/// It uses the enumerator pattern to allow usage in <c>foreach</c> loops without any heap allocations.
/// </summary>
[Experimental("WIAOJ_SPNSPLTTR")]
public ref struct SpanSplitter {
    private readonly ReadOnlySpan<char> _original;
    private ReadOnlySpan<char> _remaining;
    private int _consumedCount;
    private readonly char _separator;
    private readonly SearchValues<char>? _searchValues;
    private readonly bool _removeEmptyEntries;
    private bool _isStarted;
    private bool _hasTrailingEmpty;
    private int _currentStart;
    private int _currentEnd;

    /// <summary>
    /// Initializes a splitter for a single character separator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanSplitter(ReadOnlySpan<char> span, char separator, bool removeEmptyEntries = false) {
        this._original = span;
        this._remaining = span;
        this._separator = separator;
        this._searchValues = null;
        this._removeEmptyEntries = removeEmptyEntries;
        this._isStarted = false;
        this._hasTrailingEmpty = false;
    }

    /// <summary>
    /// Initializes a splitter for multiple character separators using optimized <see cref="SearchValues{char}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanSplitter(ReadOnlySpan<char> span, SearchValues<char> searchValues, bool removeEmptyEntries = false) {
        this._original = span;
        this._remaining = span;
        this._searchValues = searchValues;
        this._separator = default;
        this._removeEmptyEntries = removeEmptyEntries;
        this._isStarted = false;
        this._hasTrailingEmpty = false;
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
    public readonly SplitEntry Current => new(this._original[this._currentStart..this._currentEnd], this._currentStart, this._currentEnd);

    /// <summary>
    /// Advances the enumerator to the next segment of the span.
    /// </summary>
    /// <returns><see langword="true"/> if the enumerator was successfully advanced; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() {
        // İlk çalıştırma kontrolü
        if(!this._isStarted) {
            this._isStarted = true;
            if(this._remaining.IsEmpty) return false;
        }

        // Kalan veri bittiyse son durumu kontrol et (Trailing Empty)
        if(this._remaining.IsEmpty) {
            if(this._hasTrailingEmpty && !this._removeEmptyEntries) {
                this._currentStart = this._consumedCount;
                this._currentEnd = this._consumedCount;
                this._hasTrailingEmpty = false; // Bir sonraki çağrıda false dönmesi için
                return true;
            }
            return false;
        }

        // Ana Döngü (while true yerine daha doğrudan bir yapı)
        do {
            // Ayırıcıyı bul (En sıcak satır)
            int index = this._searchValues != null
                ? this._remaining.IndexOfAny(this._searchValues)
                : this._remaining.IndexOf(this._separator);

            if(index != -1) {
                // Ayırıcı bulundu (Fast Path)
                this._currentStart = this._consumedCount;
                this._currentEnd = this._consumedCount + index;

                // Durumu güncelle
                int advance = index + 1;
                this._remaining = this._remaining[advance..];
                this._consumedCount += advance;
                this._hasTrailingEmpty = true;

                // Boş girişleri atlamıyorsak veya parça boş değilse dön
                if(!this._removeEmptyEntries || this._currentStart != this._currentEnd) {
                    return true;
                }

                // Buraya geldiyse _removeEmptyEntries true'dur ve parça boştur, 
                // do-while sayesinde otomatik başa döner (Continue mantığı)
            }
            else {
                // Ayırıcı bulunamadı (Son parça)
                this._currentStart = this._consumedCount;
                this._currentEnd = this._consumedCount + this._remaining.Length;
                this._remaining = default;
                this._hasTrailingEmpty = false;

                // Eğer son parça boşsa ve boşları atlamak istiyorsak bitti
                return !(this._removeEmptyEntries && this._currentStart == this._currentEnd);
            }
        } while(!this._remaining.IsEmpty);

        // Eğer döngüden çıkıldıysa ve hala dönmemiz gereken bir trailing empty varsa
        if(this._hasTrailingEmpty && !this._removeEmptyEntries) {
            this._currentStart = this._consumedCount;
            this._currentEnd = this._consumedCount;
            this._hasTrailingEmpty = false;
            return true;
        }

        return false;
    }
}
public readonly ref struct SplitEntry {
    // Span'ı burada hazır (dilimlenmiş) halde tutuyoruz
    public readonly ReadOnlySpan<char> Value;
    public readonly int Start;
    public readonly int End;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SplitEntry(ReadOnlySpan<char> value, int start, int end) {
        this.Value = value; // Dilimlenmiş veri zaten geliyor, sadece kopyalıyoruz
        this.Start = start;
        this.End = end;
    }

    public Range Range => new(this.Start, this.End);

    // Artık bu operatör sadece bir field (alan) okuması yapacak
    // İşlemci seviyesinde sadece 1-2 komut (mov) sürecek
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<char>(SplitEntry entry) {
        return entry.Value;
    }
}

[Experimental("WIAOJ_SPNSPLTTR")]
public static class SpanSplitterExtensions {
    /// <summary>Splits a string into segments without allocating new strings or arrays.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanSplitter SplitValue(this string? s, char separator) {
        return new(s.AsSpan(), separator, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanSplitter SplitValue(this string? s, char separator, bool removeEmptyEntries) {
        return new(s.AsSpan(), separator, removeEmptyEntries);
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