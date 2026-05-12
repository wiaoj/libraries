using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives.Buffers;
/// <summary>
/// Provides static factory methods for <see cref="ValueBuffer{T}"/>.
/// </summary>
public static class ValueBuffer {
    /// <summary>
    /// Rents a buffer from the shared ArrayPool without requiring a stack-allocated initial buffer.
    /// </summary>
    /// <typeparam name="T">The type of items in the buffer.</typeparam>
    /// <param name="minimumLength">The minimum required length of the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<T> Rent<T>(int minimumLength) where T : unmanaged {
        return new ValueBuffer<T>(minimumLength, default);
    }

    /// <inheritdoc cref="Rent{T}(int)"/>
    /// <param name="minimumLength">The total required length.</param> 
    /// <param name="onDispose">
    /// A callback invoked with the active span when the buffer is disposed,
    /// before the rented array is cleared and returned to the pool.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<T> Rent<T>(int minimumLength, Action<Span<T>> onDispose) where T : unmanaged {
        return new ValueBuffer<T>(minimumLength, default, onDispose);
    }

    /// <summary>Rents a byte buffer from the shared ArrayPool.</summary>
    /// <param name="minimumLength">The minimum required length of the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<byte> Rent(int minimumLength) {
        return new ValueBuffer<byte>(minimumLength, default);
    }

    /// <inheritdoc cref="Rent(int)"/>
    /// <param name="minimumLength">The total required length.</param> 
    /// <param name="onDispose">
    /// A callback invoked with the active span when the buffer is disposed,
    /// before the rented array is cleared and returned to the pool.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<byte> Rent(int minimumLength, Action<Span<byte>> onDispose) {
        return new ValueBuffer<byte>(minimumLength, default, onDispose);
    }

    /// <summary>Rents a char buffer from the shared ArrayPool.</summary>
    /// <param name="minimumLength">The minimum required length of the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<char> RentChar(int minimumLength) {
        return new ValueBuffer<char>(minimumLength, default);
    }

    /// <inheritdoc cref="RentChar(int)"/>
    /// <param name="minimumLength">The total required length.</param> 
    /// <param name="onDispose">
    /// A callback invoked with the active span when the buffer is disposed,
    /// before the rented array is cleared and returned to the pool.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<char> RentChar(int minimumLength, Action<Span<char>> onDispose) {
        return new ValueBuffer<char>(minimumLength, default, onDispose);
    }

    /// <summary>
    /// Creates a hybrid buffer using the provided span if sufficient, otherwise rents from the ArrayPool.
    /// </summary>
    /// <typeparam name="T">The type of items in the buffer.</typeparam>
    /// <param name="minimumLength">The total required length.</param>
    /// <param name="initialBuffer">A stack-allocated buffer to use if possible.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<T> Create<T>(int minimumLength, Span<T> initialBuffer) where T : unmanaged {
        return new ValueBuffer<T>(minimumLength, initialBuffer);
    }

    /// <inheritdoc cref="Create{T}(int, Span{T})"/>
    /// <param name="minimumLength">The total required length.</param>
    /// <param name="initialBuffer">A stack-allocated buffer to use if possible.</param>
    /// <param name="onDispose">
    /// A callback invoked with the active span when the buffer is disposed,
    /// before the rented array is cleared and returned to the pool.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<T> Create<T>(int minimumLength, Span<T> initialBuffer, Action<Span<T>> onDispose) where T : unmanaged {
        return new ValueBuffer<T>(minimumLength, initialBuffer, onDispose);
    }

    /// <summary>Creates a hybrid byte buffer using the provided span if sufficient, otherwise rents.</summary>
    /// <param name="minimumLength">The total required length.</param>
    /// <param name="initialBuffer">A stack-allocated buffer to use if possible.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<byte> Create(int minimumLength, Span<byte> initialBuffer) {
        return new ValueBuffer<byte>(minimumLength, initialBuffer);
    }

    /// <inheritdoc cref="Create(int, Span{byte})"/>
    /// <param name="minimumLength">The total required length.</param>
    /// <param name="initialBuffer">A stack-allocated buffer to use if possible.</param>
    /// <param name="onDispose">
    /// A callback invoked with the active span when the buffer is disposed,
    /// before the rented array is cleared and returned to the pool.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<byte> Create(int minimumLength, Span<byte> initialBuffer, Action<Span<byte>> onDispose) {
        return new ValueBuffer<byte>(minimumLength, initialBuffer, onDispose);
    }

    /// <summary>Creates a hybrid char buffer using the provided span if sufficient, otherwise rents.</summary>
    /// <param name="minimumLength">The total required length.</param>
    /// <param name="initialBuffer">A stack-allocated buffer to use if possible.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<char> CreateChar(int minimumLength, Span<char> initialBuffer) {
        return new ValueBuffer<char>(minimumLength, initialBuffer);
    }

    /// <inheritdoc cref="CreateChar(int, Span{char})"/>
    /// <param name="minimumLength">The total required length.</param>
    /// <param name="initialBuffer">A stack-allocated buffer to use if possible.</param>
    /// <param name="onDispose">
    /// A callback invoked with the active span when the buffer is disposed,
    /// before the rented array is cleared and returned to the pool.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<char> CreateChar(int minimumLength, Span<char> initialBuffer, Action<Span<char>> onDispose) {
        return new ValueBuffer<char>(minimumLength, initialBuffer, onDispose);
    }

    /// <summary>Creates a 32-byte buffer.</summary>
    /// <param name="stack">The stack-allocated span of size 32.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<byte> Create32(Span<byte> stack) {
        return new(32, stack);
    }

    /// <summary>Creates a 64-byte buffer.</summary>
    /// <param name="stack">The stack-allocated span of size 64.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<byte> Create64(Span<byte> stack) {
        return new(64, stack);
    }

    /// <summary>Creates a 128-byte buffer.</summary>
    /// <param name="stack">The stack-allocated span of size 128.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<byte> Create128(Span<byte> stack) {
        return new(128, stack);
    }

    /// <summary>Creates a 256-byte buffer.</summary>
    /// <param name="stack">The stack-allocated span of size 256.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<byte> Create256(Span<byte> stack) {
        return new(256, stack);
    }

    /// <summary>Creates a 512-byte buffer.</summary>
    /// <param name="stack">The stack-allocated span of size 512.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<byte> Create512(Span<byte> stack) {
        return new(512, stack);
    }

    /// <summary>Creates a 1024-byte buffer.</summary>
    /// <param name="stack">The stack-allocated span of size 1024.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<byte> Create1024(Span<byte> stack) {
        return new(1024, stack);
    }
}

/// <summary>
/// Extension methods for seamless integration with spans.
/// </summary>
public static class ValueBufferExtensions {
    /// <summary>
    /// Wraps an existing Span into a ValueBuffer without any allocation or renting.
    /// </summary>
    /// <typeparam name="T">The type of items in the span.</typeparam>
    /// <param name="span">The source span.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<T> AsValueBuffer<T>(this Span<T> span) where T : unmanaged {
        return new ValueBuffer<T>(span);
    }

    /// <inheritdoc cref="AsValueBuffer{T}(Span{T})"/>  
    /// <param name="span">The source span.</param>
    /// <param name="onDispose">
    /// A callback invoked with the active span when the buffer is disposed.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueBuffer<T> AsValueBuffer<T>(this Span<T> span, Action<Span<T>> onDispose) where T : unmanaged {
        return new ValueBuffer<T>(span, onDispose);
    }
}