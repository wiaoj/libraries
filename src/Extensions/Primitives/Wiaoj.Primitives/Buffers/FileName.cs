using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wiaoj.Primitives.Buffers;
/// <summary>
/// A high-performance, stack-first, growable list designed for zero-allocation in hot paths.
/// It uses an initial <see cref="Span{T}"/> (usually stack-allocated) and moves to <see cref="ArrayPool{T}"/> if it exceeds initial capacity.
/// </summary>
/// <remarks>
/// Since this is a <see langword="ref struct"/>, it cannot be used in async methods, 
/// stored as a field in a class, or used with standard LINQ (IEnumerable).
/// </remarks>
/// <typeparam name="T">The type of elements in the list. Must be unmanaged to optimize memory operations.</typeparam>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
public ref struct ValueList<T> where T : /*unmanaged*/notnull {
    private Span<T> _span;
    private T[]? _rented;
    private int _pos;

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

    /// <summary>Gets a <see cref="Span{T}"/> representing the active elements of the list.</summary>
    public readonly Span<T> AsSpan() {
        return this._span[..this._pos];
    }

    /// <summary>
    /// Adds an item to the list.
    /// </summary>
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
    /// Useful for initializing large structs directly in the list memory to avoid copies.
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(params ReadOnlySpan<T> items) {
        //// Yer kalıp kalmadığını kontrol et
        //if(_pos + items.Length > _span.Length)
        //    Grow(_pos + items.Length);

        //int i = 0;
        //// T, unmanaged ve Vector tarafından destekleniyorsa (int, float, vb.)
        //if(Vector.IsHardwareAccelerated && items.Length >= Vector<T>.Count) {
        //    // İşlemcinin (AVX2/AVX512) tek seferde işleyebileceği miktar
        //    int vectorSize = Vector<T>.Count;

        //    for(; i <= items.Length - vectorSize; i += vectorSize) {
        //        // Veriyi vektör olarak oku ve hedefe tek hamlede yaz
        //        var v = new Vector<T>(items[i..]);
        //        v.CopyTo(_span[(_pos + i)..]);
        //    }
        //}

        //// Kalan elemanları (vektöre sığmayanlar) klasik yöntemle ekle
        //for(; i < items.Length; i++) {
        //    _span[_pos + i] = items[i];
        //}

        //_pos += items.Length;

        if(items.IsEmpty) return;

        int requiredCapacity = _pos + items.Length;
        if((uint)requiredCapacity > (uint)_span.Length) {
            Grow(requiredCapacity);
        }

        items.CopyTo(_span[_pos..]);

        _pos += items.Length;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAdd(T item) {
        Grow(this._pos + 1);
        this._span[this._pos++] = item;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAddRange(ReadOnlySpan<T> items) {
        Grow(this._pos + items.Length);
        items.CopyTo(this._span[this._pos..]);
        this._pos += items.Length;
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
            // Since T is unmanaged, we don't need to clear the array for security/GC.
            ArrayPool<T>.Shared.Return(toReturn, clearArray: false);
        }
    }

    /// <summary>
    /// Clears the list without releasing the internal buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() {
        this._pos = 0;
    }

    /// <summary>
    /// Returns the rented buffer to the pool. Must be called if the list might have grown.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public void Dispose() {
    //    T[]? toReturn = this._rented;
    //    if(toReturn != null) {
    //        this._rented = null;
    //        ArrayPool<T>.Shared.Return(toReturn, clearArray: false);
    //    }
    //}
    public void Dispose() {
        // Eğer T bir referans tipiyse veya içinde referans barındırıyorsa
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
            // Diziyi sıfırla ki GC nesneleri toplayabilsin
            _span.Clear();
        }

        // Diziyi havuza geri ver
        if(_rented != null) {
            ArrayPool<T>.Shared.Return(_rented);
            _rented = null;
        }
    }

    /// <summary>
    /// Returns a reference to the element at the specified index.
    /// </summary>
    public readonly ref T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref this._span[index];
    }

    /// <summary>Returns an enumerator for the list.</summary>
    public readonly Span<T>.Enumerator GetEnumerator() {
        return this._span[..this._pos].GetEnumerator();
    }
}