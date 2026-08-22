using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Wiaoj.Concurrency;

/// <summary>
/// Provides a comprehensive set of high-performance, type-safe, and aggressively inlined atomic operations,
/// serving as an optimized zero-cost wrapper over <see cref="Interlocked"/> and <see cref="Volatile"/>.
/// </summary>
#if WIAOJ_PRIMITIVES
internal static class Atomic {
#else
public static class Atomic {
#endif

    #region Volatile Read/Write Operations

    /// <summary>
    /// Reads the value from a specified location, ensuring the latest value is retrieved from main memory.
    /// </summary>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(location))]
    public static T Read<T>([NotNullIfNotNull(nameof(location))] ref readonly T location) where T : class? {
        return Volatile.Read(in location);
    }

    /// <summary>
    /// Writes a value to a specified location, ensuring it is immediately visible to all threads.
    /// </summary>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(ref T location, T value) where T : class? {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Volatile.Read(ref readonly byte)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Read(ref readonly byte location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Volatile.Write(ref byte, byte)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref byte location, byte value) {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Volatile.Read(ref readonly int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Read(ref readonly int location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Volatile.Write(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref int location, int value) {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Volatile.Read(ref readonly uint)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Read(ref readonly uint location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Volatile.Write(ref uint, uint)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref uint location, uint value) {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Volatile.Read(ref readonly long)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Read(ref readonly long location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Volatile.Write(ref long, long)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref long location, long value) {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Volatile.Read(ref readonly ulong)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Read(ref readonly ulong location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Volatile.Write(ref ulong, ulong)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref ulong location, ulong value) {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Volatile.Read(ref readonly bool)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Read(ref readonly bool location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Volatile.Write(ref bool, bool)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref bool location, bool value) {
        Volatile.Write(ref location, value);
    }

    #endregion

    #region Interlocked Operations for Reference Types

    /// <summary>
    /// Atomically exchanges the value at a specified location with a new value.
    /// </summary>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(location))]
    public static T? Exchange<T>(ref T? location, T? value) where T : class {
        return Interlocked.Exchange(ref location, value);
    }

    /// <inheritdoc cref="Interlocked.Exchange(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Exchange(ref int location, int value) {
        return Interlocked.Exchange(ref location, value);
    }

    /// <inheritdoc cref="Interlocked.Exchange(ref long, long)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Exchange(ref long location, long value) {
        return Interlocked.Exchange(ref location, value);
    }

    /// <summary>
    /// Atomically compares two instances of reference types for equality and, if equal, replaces the first one.
    /// </summary>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(location))]
    public static T CompareExchange<T>(ref T location, T value, T comparand) where T : class? {
        return Interlocked.CompareExchange(ref location, value, comparand);
    }

    /// <summary>
    /// Atomically sets a field to a specified value if its current value is equal to a comparand.
    /// </summary>
    /// <returns><see langword="true"/> if the exchange succeeded; otherwise, <see langword="false"/>.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange<T>(ref T? location, T? value, T? comparand) where T : class? {
        return Interlocked.CompareExchange(ref location, value, comparand) == comparand;
    }

    /// <summary>
    /// Atomically sets a byte field to a specified value if its current value is equal to a comparand.
    /// </summary>
    /// <returns><see langword="true"/> if the exchange succeeded; otherwise, <see langword="false"/>.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref byte location, byte value, byte comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand) == comparand;
    }

    /// <summary>
    /// Atomically sets an int field to a specified value if its current value is equal to a comparand.
    /// </summary>
    /// <returns><see langword="true"/> if the exchange succeeded; otherwise, <see langword="false"/>.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref int location, int value, int comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand) == comparand;
    }

    /// <summary>
    /// Atomically sets a long field to a specified value if its current value is equal to a comparand.
    /// </summary>
    /// <returns><see langword="true"/> if the exchange succeeded; otherwise, <see langword="false"/>.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref long location, long value, long comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand) == comparand;
    }

    /// <summary>
    /// Atomically takes the value of a specified field and replaces it with <see langword="null"/>.
    /// </summary>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(location))]
    public static T? Take<T>(ref T? location) where T : class {
        return Interlocked.Exchange(ref location, null);
    }

    #endregion

    #region Lock-Free Compare-And-Swap (CAS) Update Operations

    /// <summary>
    /// Atomically updates a reference field using a lock-free Compare-And-Swap loop.
    /// </summary>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Update<T>(ref T location, Func<T, T> updateFunction) where T : class {
        T initialValue, newValue;
        do {
            initialValue = Volatile.Read(ref location);
            newValue = updateFunction(initialValue);
        } while(Interlocked.CompareExchange(ref location, newValue, initialValue) != initialValue);

        return newValue;
    }

    /// <summary>
    /// Atomically updates a reference field with state to prevent closure allocations.
    /// </summary>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Update<T, TState>(ref T location, TState state, Func<T, TState, T> updateFunction) where T : class {
        T initialValue, newValue;
        do {
            initialValue = Volatile.Read(ref location);
            newValue = updateFunction(initialValue, state);
        } while(Interlocked.CompareExchange(ref location, newValue, initialValue) != initialValue);

        return newValue;
    }

    /// <summary>
    /// Atomically updates an integer field using a lock-free Compare-And-Swap loop.
    /// </summary>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Update(ref int location, Func<int, int> updateFunction) {
        int initialValue, newValue;
        do {
            initialValue = Volatile.Read(ref location);
            newValue = updateFunction(initialValue);
        } while(Interlocked.CompareExchange(ref location, newValue, initialValue) != initialValue);

        return newValue;
    }

    /// <summary>
    /// Atomically updates a 64-bit integer field using a lock-free Compare-And-Swap loop.
    /// </summary>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Update(ref long location, Func<long, long> updateFunction) {
        long initialValue, newValue;
        do {
            initialValue = Interlocked.Read(ref location);
            newValue = updateFunction(initialValue);
        } while(Interlocked.CompareExchange(ref location, newValue, initialValue) != initialValue);

        return newValue;
    }

    #endregion

    #region Interlocked Numeric Operations

    /// <inheritdoc cref="Interlocked.Increment(ref int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Increment(ref int location) {
        return Interlocked.Increment(ref location);
    }

    /// <inheritdoc cref="Interlocked.Decrement(ref int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Decrement(ref int location) {
        return Interlocked.Decrement(ref location);
    }

    /// <inheritdoc cref="Interlocked.Add(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Add(ref int location, int value) {
        return Interlocked.Add(ref location, value);
    }

    /// <inheritdoc cref="Interlocked.CompareExchange(ref int, int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareExchange(ref int location, int value, int comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand) == comparand;
    }

    /// <inheritdoc cref="Interlocked.Increment(ref long)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Increment(ref long location) {
        return Interlocked.Increment(ref location);
    }

    /// <inheritdoc cref="Interlocked.Decrement(ref long)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Decrement(ref long location) {
        return Interlocked.Decrement(ref location);
    }

    /// <inheritdoc cref="Interlocked.Add(ref long, long)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Add(ref long location, long value) {
        return Interlocked.Add(ref location, value);
    }

    /// <inheritdoc cref="Interlocked.CompareExchange(ref long, long, long)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareExchange(ref long location, long value, long comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand) == comparand;
    }

    #endregion
}