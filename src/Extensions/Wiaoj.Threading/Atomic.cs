using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Wiaoj.Concurrency; // Convert to Wiaoj.Threading classlib, DisposeState.cs & LifecycleManager

/// <summary>
/// Provides a comprehensive set of static, type-safe, and intuitive methods for performing atomic operations,
/// acting as a high-level wrapper around the <see cref="Interlocked"/> and <see cref="Volatile"/> classes.
/// </summary>
#if WIAOJ_PRIMITIVES
internal static class Atomic {
#else
public static class Atomic {
#endif   
    #region Volatile Read/Write Operations

    /// <summary>
    /// Reads the value from a specified location, ensuring the latest value is retrieved from main memory.
    /// This is a generic, type-safe wrapper around <see cref="Volatile.Read{T}(ref readonly T)"/>.
    /// </summary>
    [return: NotNullIfNotNull(nameof(location))]
    public static T Read<T>([NotNullIfNotNull(nameof(location))] ref readonly T location) where T : class? {
        return Volatile.Read(in location);
    }

    /// <summary>
    /// Writes a value to a specified location, ensuring it is immediately visible to all threads.
    /// This is a generic, type-safe wrapper around <see cref="Volatile.Write{T}(ref T, T)"/>.
    /// </summary>
    public static void Write<T>(ref T location, T value) where T : class? {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Volatile.Read(ref readonly int)"/>
    public static int Read(ref readonly int location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Volatile.Write(ref int, int)"/>
    public static void Write(ref int location, int value) {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Volatile.Read(ref readonly long)"/>
    public static long Read(ref readonly long location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Volatile.Write(ref long, long)"/>
    public static void Write(ref long location, long value) {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Volatile.Read(ref readonly bool)"/>
    public static bool Read(ref readonly bool location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Volatile.Write(ref bool, bool)"/>
    public static void Write(ref bool location, bool value) {
        Volatile.Write(ref location, value);
    }

    #endregion

    #region Interlocked Operations for Reference Types
    /// <summary>
    /// Atomically exchanges the value at a specified location with a new value.
    /// </summary>
    /// <typeparam name="T">The type of the field. Must be a reference type.</typeparam>
    /// <param name="location">The field to exchange the value of.</param>
    /// <param name="value">The new value to store.</param>
    /// <returns>The original value that was at the location before the exchange.</returns>
    [return: NotNullIfNotNull(nameof(location))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? Exchange<T>(ref T? location, T? value) where T : class {
        return Interlocked.Exchange(ref location, value);
    }

    /// <inheritdoc cref="Interlocked.Exchange(ref int, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Exchange(ref int location, int value) {
        return Interlocked.Exchange(ref location, value);
    }

    /// <inheritdoc cref="Interlocked.Exchange(ref long, long)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Exchange(ref long location, long value) {
        return Interlocked.Exchange(ref location, value);
    }

    /// <summary>
    /// Atomically sets a field to a specified value if its current value is equal to a comparand.
    /// This is a more intuitive, boolean-returning wrapper around <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>.
    /// </summary>
    /// <typeparam name="T">The type of the objects to compare and set. Must be a reference type.</typeparam>
    /// <param name="location">The destination field whose value is compared with <paramref name="comparand"/> and possibly replaced with <paramref name="value"/>.</param>
    /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
    /// <param name="comparand">The value that is compared to the value at <paramref name="location"/>.</param>
    /// <returns><see langword="true"/> if the exchange succeeded; otherwise, <see langword="false"/>.</returns>
    [return: NotNullIfNotNull(nameof(location))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool CompareExchange<T>(ref T? location, T? value, T? comparand) where T : class {
        return Interlocked.CompareExchange(ref location, value, comparand) == comparand;
    }

    /// <summary>
    /// Atomically takes the value of a specified field and replaces it with <see langword="null"/>.
    /// This provides a "claim" semantic, ensuring only one thread can get the non-null value.
    /// </summary>
    /// <typeparam name="T">The type of the field. Must be a reference type.</typeparam>
    /// <param name="location">The field to take the value from.</param>
    /// <returns>The original value of the field before it was set to <see langword="null"/>.</returns>

    [return: NotNullIfNotNull(nameof(location))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T? Take<T>(ref T? location) where T : class {
        return Interlocked.Exchange(ref location, null);
    }

    /// <summary>
    /// Atomically updates a field by applying a specified update function in a lock-free manner.
    /// </summary>
    /// <typeparam name="T">The type of the field. Must be a reference type.</typeparam>
    /// <param name="location">The field to update.</param>
    /// <param name="updateFunction">The function to apply to the current value to compute the new value.</param>
    /// <returns>The new value that was set in the field.</returns>
    public static T Update<T>(ref T location, Func<T, T> updateFunction) where T : class {
        T initialValue, newValue;
        do {
            initialValue = Volatile.Read(ref location);
            newValue = updateFunction(initialValue);
        } while (Interlocked.CompareExchange(ref location, newValue, initialValue) != initialValue);

        return newValue;
    }

    #endregion

    #region Interlocked Operations for Integers

    /// <inheritdoc cref="Interlocked.Increment(ref int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Increment(ref int location) {
        return Interlocked.Increment(ref location);
    }

    /// <inheritdoc cref="Interlocked.Decrement(ref int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Decrement(ref int location) {
        return Interlocked.Decrement(ref location);
    }

    /// <inheritdoc cref="Interlocked.Add(ref int, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Add(ref int location, int value) {
        return Interlocked.Add(ref location, value);
    }

    /// <inheritdoc cref="Interlocked.CompareExchange(ref int, int, int)"/>
    /// <returns><see langword="true"/> if the exchange succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareExchange(ref int location, int value, int comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand) == comparand;
    }

    /// <inheritdoc cref="Interlocked.Increment(ref long)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Increment(ref long location) {
        return Interlocked.Increment(ref location);
    }

    /// <inheritdoc cref="Interlocked.Decrement(ref long)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Decrement(ref long location) {
        return Interlocked.Decrement(ref location);
    }

    /// <inheritdoc cref="Interlocked.Add(ref long, long)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Add(ref long location, long value) {
        return Interlocked.Add(ref location, value);
    }

    /// <inheritdoc cref="Interlocked.CompareExchange(ref long, long, long)"/>
    /// <returns><see langword="true"/> if the exchange succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareExchange(ref long location, long value, long comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand) == comparand;
    }

    #endregion
}