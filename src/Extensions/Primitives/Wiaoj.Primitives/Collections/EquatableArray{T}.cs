using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Wiaoj.Primitives.Collections;

/// <summary>
/// Represents an immutable array structure focused on zero-allocation and content-based value equality.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>[JsonConverter(typeof(EquatableArrayJsonConverterFactory))] 
[CollectionBuilder(typeof(EquatableArray), nameof(EquatableArray.Create))]
[JsonConverter(typeof(EquatableArrayJsonConverterFactory))]
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T> {

    private readonly ImmutableArray<T> _items;

    /// <summary>
    /// Gets an empty <see cref="EquatableArray{T}"/> instance containing no elements.
    /// </summary>
    public static EquatableArray<T> Empty { get; } = new(ImmutableArray<T>.Empty);

    /// <summary>
    /// Initializes a new <see cref="EquatableArray{T}"/> from the specified standard array (<see cref="T:T[]"/>).
    /// Provides optimization over the generic IEnumerable constructor.
    /// </summary>
    /// <param name="items">The array containing the elements. If <see langword="null"/>, an empty collection is created.</param>
    public EquatableArray(T[]? items) {
        this._items = items is null ? [] : ImmutableArray.Create(items);
    }

    /// <summary>
    /// Initializes a new <see cref="EquatableArray{T}"/> from the specified <see cref="ImmutableArray{T}"/> instance.
    /// </summary>
    /// <param name="items">The immutable array to wrap. If default/uninitialized, it is initialized as empty.</param>
    public EquatableArray(ImmutableArray<T> items) {
        this._items = items.IsDefault ? [] : items;
    }

    /// <summary>
    /// Initializes a new <see cref="EquatableArray{T}"/> from the specified <see cref="IEnumerable{T}"/> collection (Fallback constructor).
    /// </summary>
    /// <param name="items">The collection of elements. If <see langword="null"/>, an empty collection is created.</param>
    public EquatableArray(IEnumerable<T>? items) {
        this._items = items switch {
            null => [],
            ImmutableArray<T> arr => arr.IsDefault ? [] : arr,
            T[] arr => ImmutableArray.Create(arr),
            _ => [.. items]
        };
    }

    /// <summary>
    /// Implicitly converts a <see cref="T:T[]"/> array to an <see cref="EquatableArray{T}"/>.
    /// </summary>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator EquatableArray<T>(T[]? array) {
        return new(array);
    }

    /// <summary>
    /// Implicitly converts a <see cref="List{T}"/> collection to an <see cref="EquatableArray{T}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator EquatableArray<T>(List<T>? list) {
        return new(list);
    }

    /// <summary>
    /// Implicitly converts an <see cref="ImmutableArray{T}"/> to an <see cref="EquatableArray{T}"/>.
    /// </summary>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator EquatableArray<T>(ImmutableArray<T> array) {
        return new(array);
    }

    /// <summary>
    /// Implicitly converts an <see cref="EquatableArray{T}"/> instance to a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(EquatableArray<T> array) {
        return array.AsSpan();
    }

    /// <summary>
    /// Implicitly converts an <see cref="EquatableArray{T}"/> instance to a <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlyMemory<T>(EquatableArray<T> array) {
        return array.AsMemory();
    }

    /// <summary>
    /// Gets the total number of elements in the collection.
    /// </summary>
    public int Count {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._items.IsDefault ? 0 : this._items.Length;
    }

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to access.</param>
    /// <returns>The element at the specified index.</returns>
    public T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._items[index];
    }

    /// <summary>
    /// Returns a specific portion of the array as a new Immutable structure, targeting zero-allocation.
    /// ImmutableArray.Slice works directly with references, ensuring optimal memory management without hidden allocations.
    /// </summary>
    /// <param name="start">The zero-based index at which to begin the slice.</param>
    /// <param name="length">The number of elements to include in the slice.</param>
    /// <returns>A new <see cref="EquatableArray{T}"/> containing the specified portion of the array.</returns>
    public EquatableArray<T> Slice(int start, int length) {
        if(this._items.IsDefaultOrEmpty) return Empty;
        return new EquatableArray<T>(this._items.Slice(start, length));
    }

    /// <summary>
    /// Determines whether the array contains the specified element.
    /// </summary>
    /// <param name="item">The element to locate in the array.</param>
    /// <returns><see langword="true"/> if the element is found in the array; otherwise, <see langword="false"/>.</returns>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) {
        return !this._items.IsDefaultOrEmpty && this._items.Contains(item);
    }

    /// <summary>
    /// Searches for the specified element and returns the zero-based index of the first occurrence within the array.
    /// </summary>
    /// <param name="item">The element to locate in the array.</param>
    /// <returns>The zero-based index of the first occurrence of the element, if found; otherwise, -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item) {
        return this._items.IsDefaultOrEmpty ? -1 : this._items.IndexOf(item);
    }

    /// <summary>
    /// Returns a read-only window (<see cref="ReadOnlySpan{T}"/>) to read the array contents with zero-allocation.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{T}"/> representing the array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> AsSpan() {
        return this._items.IsDefaultOrEmpty ? [] : this._items.AsSpan();
    }

    /// <summary>
    /// Returns a memory window (<see cref="ReadOnlyMemory{T}"/>) representing the array contents for use in asynchronous methods (e.g., Stream reads).
    /// </summary>
    /// <returns>A <see cref="ReadOnlyMemory{T}"/> representing the array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<T> AsMemory() {
        if(this._items.IsDefaultOrEmpty) return ReadOnlyMemory<T>.Empty;
        return new ReadOnlyMemory<T>(ImmutableCollectionsMarshal.AsArray(this._items)!);
    }

    /// <summary>
    /// Returns the current collection as a standard <see cref="ImmutableArray{T}"/>.
    /// </summary>
    /// <returns>The underlying <see cref="ImmutableArray{T}"/>.</returns>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableArray<T> AsImmutableArray() {
        return this._items.IsDefault ? [] : this._items;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the array.
    /// </summary>
    /// <returns>An enumerator for the <see cref="ImmutableArray{T}"/>.</returns>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableArray<T>.Enumerator GetEnumerator() {
        return AsImmutableArray().GetEnumerator();
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() {
        return ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
    }

    /// <summary>
    /// Copies the entire <see cref="EquatableArray{T}"/> to a compatible one-dimensional array, starting at the specified index of the target array.
    /// </summary>
    /// <param name="destination">The one-dimensional array that is the destination of the elements.</param>
    /// <param name="destinationIndex">The zero-based index in the destination array at which copying begins.</param>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] destination, int destinationIndex) {
        if(!this._items.IsDefaultOrEmpty) {
            this._items.CopyTo(destination, destinationIndex);
        }
    }

    /// <summary>
    /// Copies the contents of the array into a destination <see cref="Span{T}"/> using hardware-accelerated memory block copies.
    /// </summary>
    /// <param name="destination">The destination span.</param>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<T> destination) {
        if(!this._items.IsDefaultOrEmpty) {
            this._items.AsSpan().CopyTo(destination);
        }
    }

    /// <summary>
    /// Attempts to copy the current contents into a destination <see cref="Span{T}"/>.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <returns><see langword="true"/> if the operation was successful; otherwise, <see langword="false"/>.</returns>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryCopyTo(Span<T> destination) {
        if(this._items.IsDefaultOrEmpty) return true;

        return this._items.AsSpan().TryCopyTo(destination);
    }

    /// <summary>
    /// Determines whether the current <see cref="EquatableArray{T}"/> is equal to another <see cref="EquatableArray{T}"/>.
    /// Bypasses LINQ to achieve zero-allocation.
    /// </summary>
    /// <param name="other">The other array to compare with.</param>
    /// <returns><see langword="true"/> if the sequences are equal; otherwise, <see langword="false"/>.</returns>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SequenceEqual(EquatableArray<T> other) {
        return Equals(other);
    }

    /// <summary>
    /// Determines whether the current array is equal to a specified <see cref="ReadOnlySpan{T}"/>.
    /// Uses highly optimized, hardware-accelerated (SIMD) memory comparison.
    /// </summary>
    /// <param name="other">The span to compare with.</param>
    /// <returns><see langword="true"/> if the sequences are equal; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SequenceEqual(ReadOnlySpan<T> other) {
        // 1. Length Check
        if(this.Count != other.Length) return false;

        // 2. SIMD Block Comparison
        return AsSpan().SequenceEqual(other);
    }

    /// <summary>
    /// Determines whether the current array is equal to a specified <see cref="IEnumerable{T}"/>.
    /// Intercepts LINQ calls to provide zero-allocation comparisons for common types.
    /// </summary>
    /// <param name="other">The enumerable to compare with.</param>
    /// <returns><see langword="true"/> if the sequences are equal; otherwise, <see langword="false"/>.</returns>
    public bool SequenceEqual(IEnumerable<T>? other) {
        if(other is null) return false;

        if(other is EquatableArray<T> eqArray) return Equals(eqArray);

        if(other is T[] array) return SequenceEqual(array.AsSpan());

        if(other is List<T> list) {
            return SequenceEqual(CollectionsMarshal.AsSpan(list));
        }

        return Enumerable.SequenceEqual(this, other);
    }

    /// <summary>
    /// Determines whether two <see cref="EquatableArray{T}"/> structures are exactly equal by value.
    /// </summary>
    /// <param name="other">The other <see cref="EquatableArray{T}"/> to compare with.</param>
    /// <returns><see langword="true"/> if the values are identical; otherwise, <see langword="false"/>.</returns>
    public bool Equals(EquatableArray<T> other) {
        ImmutableArray<T> me = AsImmutableArray();
        ImmutableArray<T> them = other.AsImmutableArray();

        // 1. O(1) Reference Check: If they point to the exact same array, return true immediately.
        if(ImmutableCollectionsMarshal.AsArray(me) == ImmutableCollectionsMarshal.AsArray(them)) return true;

        // 2. O(1) Length Check: If lengths differ, no need to iterate over elements.
        if(me.Length != them.Length) return false;

        // 3. O(N) Value Check: Compare as fast as possible using SIMD instructions via Span Memory.
        return me.AsSpan().SequenceEqual(them.AsSpan());
    }

    /// <summary>
    /// Determines whether the current object is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> if the specified object is an <see cref="EquatableArray{T}"/> and is equal to the current array; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj) {
        return obj is EquatableArray<T> other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code for the current collection, taking the contents of the elements into account.
    /// </summary>
    /// <returns>The content-based hash code value of the array.</returns>
    public override int GetHashCode() {
        if(this._items.IsDefaultOrEmpty) return 0;

        HashCode hash = new();
        foreach(T item in this._items.AsSpan())
            hash.Add(item);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Determines whether two <see cref="EquatableArray{T}"/> objects are equal.
    /// </summary>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="EquatableArray{T}"/> objects are not equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) {
        return !left.Equals(right);
    }
}