using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wiaoj.Primitives.Buffers;

/// <summary>
/// A high-performance, stack-first, growable list designed for zero-allocation in hot paths.
/// It utilizes an initial <see cref="Span{T}"/> (typically stack-allocated via stackalloc) 
/// and transitions to <see cref="ArrayPool{T}"/> if the capacity is exceeded.
/// </summary>
/// <remarks>
/// <strong>Constraints:</strong>
/// <list type="bullet">
/// <item>As a <see langword="ref struct"/>, it cannot be used in async methods or stored on the heap.</item>
/// <item>It must be manually disposed to return rented arrays to the <see cref="ArrayPool{T}"/>.</item>
/// <item>Not compatible with standard LINQ (IEnumerable), but provides specialized zero-allocation alternatives.</item>
/// </list>
/// </remarks>
/// <typeparam name="T">The type of elements in the list. Must be non-nullable.</typeparam>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
public ref struct ValueList<T> where T : notnull {
    private Span<T> _span;
    private T[]? _rented;
    private int _pos;

    /// <summary>Gets a value indicating whether the list is empty.</summary>
    public readonly bool IsEmpty => _pos == 0;

    /// <summary>Gets the number of remaining slots in the current buffer before a resize is required.</summary>
    public readonly int RemainingCapacity => _span.Length - _pos;

    /// <summary>
    /// Initializes a new instance of <see cref="ValueList{T}"/> using the provided buffer.
    /// </summary>
    /// <param name="initialBuffer">The initial memory buffer (typically from <see langword="stackalloc"/>).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueList(Span<T> initialBuffer) {
        this._span = initialBuffer;
        this._rented = null;
        this._pos = 0;
    }

    /// <summary>Gets the number of elements currently contained in the list.</summary>
    public readonly int Count => this._pos;

    /// <summary>Gets the total capacity of the internal buffer.</summary>
    public readonly int Capacity => this._span.Length;

    /// <summary>
    /// Returns a <see cref="Span{T}"/> representing the active elements of the list.
    /// </summary>
    /// <returns>A span containing the current items.</returns>
    public readonly Span<T> AsSpan() => this._span[..this._pos];

    /// <summary>
    /// Adds an item to the list. Grows the internal buffer if necessary.
    /// </summary>
    /// <param name="item">The object to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item) {
        int pos = this._pos;
        if((uint)pos < (uint)this._span.Length) {
            Unsafe.Add(ref MemoryMarshal.GetReference(this._span), pos) = item;
            this._pos = pos + 1;
        }
        else {
            GrowAndAdd(item);
        }
    }

    /// <summary>
    /// Reserves a slot in the list and returns a reference to it.
    /// Useful for initializing large structs directly in the list memory to avoid unnecessary copies.
    /// </summary>
    /// <returns>A reference to the newly allocated slot.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T AddByRef() {
        int pos = this._pos;
        if((uint)pos >= (uint)this._span.Length) {
            Grow(pos + 1);
        }
        this._pos = pos + 1;
        return ref Unsafe.Add(ref MemoryMarshal.GetReference(this._span), pos);
    }

    /// <summary>
    /// Adds multiple items to the list from a span.
    /// </summary>
    /// <param name="items">The span of items to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(ReadOnlySpan<T> items) {
        if(items.IsEmpty) return;

        int requiredCapacity = _pos + items.Length;
        if((uint)requiredCapacity > (uint)_span.Length) {
            Grow(requiredCapacity);
        }

        items.CopyTo(_span[_pos..]);
        _pos += items.Length;
    }

    /// <summary>
    /// Ensures that the list has at least the specified capacity.
    /// </summary>
    /// <param name="capacity">The minimum capacity required.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity) {
        if(capacity > _span.Length) {
            Grow(capacity);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAdd(T item) {
        Grow(this._pos + 1);
        this._span[this._pos++] = item;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int requiredMinCapacity) {
        int newSize = Math.Max(this._span.Length == 0 ? 16 : this._span.Length * 2, requiredMinCapacity);
        T[] rented = ArrayPool<T>.Shared.Rent(newSize);

        if(this._pos > 0) {
            this._span[..this._pos].CopyTo(rented);
        }

        T[]? toReturn = this._rented;
        this._rented = rented;
        this._span = rented;

        if(toReturn != null) {
            ArrayPool<T>.Shared.Return(toReturn, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    /// <summary>
    /// Removes and returns the last element of the list.
    /// </summary>
    /// <returns>The element removed from the end of the list.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the list is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Pop() {
        if(_pos == 0) throw new InvalidOperationException("List is empty.");
        _pos--;
        T item = _span[_pos];
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
            _span[_pos] = default!;
        }
        return item;
    }

    /// <summary>
    /// Removes the first occurrence of a specific object from the list.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns><see langword="true"/> if the item was successfully removed; otherwise, <see langword="false"/>.</returns>
    /// <remarks>This is an O(n) operation using vectorized search where available.</remarks>
    public bool Remove(T item) {
        int index = this.AsSpan().IndexOf(item);
        if(index >= 0) {
            RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes the element at the specified index and shifts the subsequent elements to the left.
    /// </summary>
    /// <param name="index">The zero-based index of the element to remove.</param>
    /// <exception cref="IndexOutOfRangeException">Thrown if the index is out of range.</exception>
    public void RemoveAt(int index) {
        if((uint)index >= (uint)_pos) throw new IndexOutOfRangeException();
        _pos--;
        if(index < _pos) {
            _span[(index + 1)..(_pos + 1)].CopyTo(_span[index..]);
        }
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
            _span[_pos] = default!;
        }
    }

    /// <summary>
    /// Resets the count to zero without releasing the internal buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() {
        this._pos = 0;
    }

    /// <summary>
    /// Attempts to add an item to the list if there is remaining capacity. Does not grow the buffer.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns><see langword="true"/> if the item was added; <see langword="false"/> if the buffer is full.</returns>
    public bool TryAdd(T item) {
        if((uint)_pos < (uint)_span.Length) {
            _span[_pos++] = item;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Inserts an item into the list at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which the item should be inserted.</param>
    /// <param name="item">The item to insert.</param>
    public void Insert(int index, T item) {
        if((uint)index > (uint)_pos) throw new IndexOutOfRangeException();
        if(_pos == _span.Length) Grow(_pos + 1);

        if(index < _pos) {
            _span[index.._pos].CopyTo(_span[(index + 1)..]);
        }
        _span[index] = item;
        _pos++;
    }

    /// <summary>
    /// Returns the rented buffer to the pool and clears references if necessary.
    /// This must be called to avoid memory leaks when the list has grown.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() {
        T[]? toReturn = _rented;
        if(toReturn != null) {
            _rented = null;
            _span = default;
            ArrayPool<T>.Shared.Return(toReturn, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
        else if(RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
            _span[.._pos].Clear();
        }
    }

    /// <summary>
    /// Gets a reference to the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>A reference to the element at the specified index.</returns>
    public readonly ref T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref this._span[index];
    }

    /// <summary>Returns an enumerator that iterates through the list.</summary>
    public readonly Span<T>.Enumerator GetEnumerator() => this._span[..this._pos].GetEnumerator();

    /// <summary>
    /// Projects each element of the list into a new form and writes them to the destination span.
    /// </summary>
    /// <typeparam name="TResult">The type of the value returned by the selector.</typeparam>
    /// <param name="destination">The destination span to write results.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    public readonly void Select<TResult>(Span<TResult> destination, Func<T, TResult> selector) {
        var source = AsSpan();
        int limit = Math.Min(source.Length, destination.Length);
        for(int i = 0; i < limit; i++) {
            destination[i] = selector(source[i]);
        }
    }

    /// <summary>
    /// Provides a zero-allocation, lazy projection of the list elements.
    /// </summary>
    /// <typeparam name="TResult">The type of the value returned by the selector.</typeparam>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <returns>A <see cref="SelectEnumerator{TResult}"/> for iteration.</returns>
    public readonly SelectEnumerator<TResult> SelectLazy<TResult>(Func<T, TResult> selector) {
        return new SelectEnumerator<TResult>(AsSpan(), selector);
    }

    /// <summary>Implicitly converts the list to a <see cref="ReadOnlySpan{T}"/>.</summary>
    public static implicit operator ReadOnlySpan<T>(ValueList<T> list) => list.AsSpan();

    /// <summary>Implicitly converts the list to a <see cref="Span{T}"/>.</summary>
    public static implicit operator Span<T>(ValueList<T> list) => list.AsSpan();

    /// <summary>
    /// A zero-allocation enumerator that projects elements lazily.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected value.</typeparam>
    public ref struct SelectEnumerator<TResult> {
        private readonly Span<T> _source;
        private readonly Func<T, TResult> _selector;
        private int _index;

        internal SelectEnumerator(Span<T> source, Func<T, TResult> selector) {
            _source = source;
            _selector = selector;
            _index = -1;
        }

        /// <summary>Advances the enumerator to the next element.</summary>
        public bool MoveNext() => ++_index < _source.Length;

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly TResult Current => _selector(_source[_index]);

        /// <summary>Returns the enumerator itself for use in foreach loops.</summary>
        public readonly SelectEnumerator<TResult> GetEnumerator() => this;
    }
}