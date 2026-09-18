using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Wiaoj.Concurrency;

/// <summary>
/// Provides a comprehensive set of high-performance, type-safe, and aggressively inlined atomic operations,
/// serving as an optimized zero-cost wrapper over <see cref="Interlocked"/> and <see cref="Volatile"/>.
/// </summary>
/// <remarks>
/// All members are thread-safe. Methods in this type never introduce allocations, with the sole exception of
/// the delegate/closure allocations that the caller supplies to the <c>Update</c> family.
/// </remarks>
#if WIAOJ_PRIMITIVES
internal static class Atomic {
#else
public static class Atomic {
#endif

    #region Volatile Read/Write Operations

    /// <summary>
    /// Reads a reference from the specified location, acquiring the latest published value.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <param name="location">The location to read from.</param>
    /// <returns>The value that was read from <paramref name="location"/>.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(location))]
    public static T Read<T>(ref readonly T location) where T : class? {
        return Volatile.Read(in location);
    }

    /// <summary>
    /// Writes a reference to the specified location, publishing it so that it becomes visible to other threads.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <param name="location">The location to write to.</param>
    /// <param name="value">The value to write.</param>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(ref T location, T value) where T : class? {
        Volatile.Write(ref location, value);
    }

    /// <summary>Reads the value from the specified location with acquire semantics.</summary>
    /// <param name="location">The location to read from.</param>
    /// <returns>The value that was read.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Read(ref readonly bool location) {
        return Volatile.Read(in location);
    }

    /// <summary>Writes the value to the specified location with release semantics.</summary>
    /// <param name="location">The location to write to.</param>
    /// <param name="value">The value to write.</param>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref bool location, bool value) {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Read(ref readonly bool)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Read(ref readonly byte location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Write(ref bool, bool)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref byte location, byte value) {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Read(ref readonly bool)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Read(ref readonly int location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Write(ref bool, bool)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref int location, int value) {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Read(ref readonly bool)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Read(ref readonly uint location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Write(ref bool, bool)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref uint location, uint value) {
        Volatile.Write(ref location, value);
    }

    /// <summary>Reads the 64-bit value from the specified location with acquire semantics.</summary>
    /// <param name="location">The location to read from.</param>
    /// <returns>The value that was read.</returns>
    /// <remarks>
    /// A 64-bit read is only guaranteed to be free of tearing on 64-bit platforms, or when the field is
    /// naturally aligned. In code that must also run on 32-bit runtimes, prefer <see cref="Interlocked"/>'s
    /// <c>Read</c> method instead, which is guaranteed atomic on every platform.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Read(ref readonly long location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Write(ref bool, bool)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref long location, long value) {
        Volatile.Write(ref location, value);
    }

    /// <inheritdoc cref="Read(ref readonly long)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Read(ref readonly ulong location) {
        return Volatile.Read(in location);
    }

    /// <inheritdoc cref="Write(ref bool, bool)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ref ulong location, ulong value) {
        Volatile.Write(ref location, value);
    }

    /// <summary>
    /// Synchronizes memory access, preventing the processor from reordering reads and writes across this point.
    /// </summary>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Barrier() {
        Thread.MemoryBarrier();
    }

    #endregion

    #region Interlocked Exchange Operations

    /// <summary>
    /// Atomically sets a reference to the specified value and returns the previous value.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <param name="location">The location whose value is to be replaced.</param>
    /// <param name="value">The value to store at <paramref name="location"/>.</param>
    /// <returns>The value that was at <paramref name="location"/> before the exchange.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? Exchange<T>(ref T? location, T? value) where T : class {
        return Interlocked.Exchange(ref location, value);
    }

    /// <inheritdoc cref="Exchange{T}(ref T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Exchange(ref int location, int value) {
        return Interlocked.Exchange(ref location, value);
    }

    /// <inheritdoc cref="Exchange{T}(ref T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Exchange(ref uint location, uint value) {
        return Interlocked.Exchange(ref location, value);
    }

    /// <inheritdoc cref="Exchange{T}(ref T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Exchange(ref long location, long value) {
        return Interlocked.Exchange(ref location, value);
    }

    /// <inheritdoc cref="Exchange{T}(ref T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Exchange(ref ulong location, ulong value) {
        return Interlocked.Exchange(ref location, value);
    }

    /// <summary>
    /// Atomically takes the value at the specified location, replacing it with <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <param name="location">The location to clear.</param>
    /// <returns>
    /// The value that was at <paramref name="location"/>, or <see langword="null"/> if it had already been taken.
    /// </returns>
    /// <remarks>
    /// This is the canonical way to claim ownership of a disposable field exactly once, so that only the
    /// caller that observes a non-<see langword="null"/> result performs the disposal.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? Take<T>(ref T? location) where T : class {
        return Interlocked.Exchange(ref location, null);
    }

    #endregion

    #region Interlocked Compare-Exchange Operations

    /// <summary>
    /// Atomically compares the value at the specified location with <paramref name="comparand"/> and,
    /// if they are equal, replaces it with <paramref name="value"/>.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <param name="location">The location whose value is compared and possibly replaced.</param>
    /// <param name="value">The value that replaces the current value if the comparison succeeds.</param>
    /// <param name="comparand">The value that is compared against the current value.</param>
    /// <returns>
    /// The original value at <paramref name="location"/>. The exchange succeeded if this equals
    /// <paramref name="comparand"/>.
    /// </returns>
    /// <remarks>Reference types are compared by reference identity, not by <see cref="object.Equals(object)"/>.</remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T CompareExchange<T>(ref T location, T value, T comparand) where T : class? {
        return Interlocked.CompareExchange(ref location, value, comparand);
    }

    /// <inheritdoc cref="CompareExchange{T}(ref T, T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte CompareExchange(ref byte location, byte value, byte comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand);
    }

    /// <inheritdoc cref="CompareExchange{T}(ref T, T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CompareExchange(ref int location, int value, int comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand);
    }

    /// <inheritdoc cref="CompareExchange{T}(ref T, T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint CompareExchange(ref uint location, uint value, uint comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand);
    }

    /// <inheritdoc cref="CompareExchange{T}(ref T, T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long CompareExchange(ref long location, long value, long comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand);
    }

    /// <inheritdoc cref="CompareExchange{T}(ref T, T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong CompareExchange(ref ulong location, ulong value, ulong comparand) {
        return Interlocked.CompareExchange(ref location, value, comparand);
    }

    /// <summary>
    /// Atomically sets the value at the specified location to <paramref name="value"/> if its current value
    /// is <paramref name="comparand"/>.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <param name="location">The location whose value is compared and possibly replaced.</param>
    /// <param name="value">The value that replaces the current value if the comparison succeeds.</param>
    /// <param name="comparand">The value that is compared against the current value.</param>
    /// <returns><see langword="true"/> if the exchange succeeded; otherwise, <see langword="false"/>.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange<T>(ref T? location, T? value, T? comparand) where T : class {
        return TryCompareExchange(ref location, value, comparand, out _);
    }

    /// <summary>
    /// Atomically sets the value at the specified location to <paramref name="value"/> if its current value
    /// is <paramref name="comparand"/>, reporting the value that was observed.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <param name="location">The location whose value is compared and possibly replaced.</param>
    /// <param name="value">The value that replaces the current value if the comparison succeeds.</param>
    /// <param name="comparand">The value that is compared against the current value.</param>
    /// <param name="current">
    /// The value that was observed at <paramref name="location"/>. On failure this is the value that caused the
    /// comparison to fail, which can be fed straight back into the next attempt of a retry loop without re-reading.
    /// </param>
    /// <returns><see langword="true"/> if the exchange succeeded; otherwise, <see langword="false"/>.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange<T>(ref T? location, T? value, T? comparand, out T? current) where T : class {
        current = Interlocked.CompareExchange(ref location, value, comparand);
        return ReferenceEquals(current, comparand);
    }

    /// <inheritdoc cref="TryCompareExchange{T}(ref T, T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref byte location, byte value, byte comparand) {
        return TryCompareExchange(ref location, value, comparand, out _);
    }

    /// <inheritdoc cref="TryCompareExchange{T}(ref T, T, T, out T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref byte location, byte value, byte comparand, out byte current) {
        current = Interlocked.CompareExchange(ref location, value, comparand);
        return current == comparand;
    }

    /// <inheritdoc cref="TryCompareExchange{T}(ref T, T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref int location, int value, int comparand) {
        return TryCompareExchange(ref location, value, comparand, out _);
    }

    /// <inheritdoc cref="TryCompareExchange{T}(ref T, T, T, out T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref int location, int value, int comparand, out int current) {
        current = Interlocked.CompareExchange(ref location, value, comparand);
        return current == comparand;
    }

    /// <inheritdoc cref="TryCompareExchange{T}(ref T, T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref uint location, uint value, uint comparand) {
        return TryCompareExchange(ref location, value, comparand, out _);
    }

    /// <inheritdoc cref="TryCompareExchange{T}(ref T, T, T, out T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref uint location, uint value, uint comparand, out uint current) {
        current = Interlocked.CompareExchange(ref location, value, comparand);
        return current == comparand;
    }

    /// <inheritdoc cref="TryCompareExchange{T}(ref T, T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref long location, long value, long comparand) {
        return TryCompareExchange(ref location, value, comparand, out _);
    }

    /// <inheritdoc cref="TryCompareExchange{T}(ref T, T, T, out T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref long location, long value, long comparand, out long current) {
        current = Interlocked.CompareExchange(ref location, value, comparand);
        return current == comparand;
    }

    /// <inheritdoc cref="TryCompareExchange{T}(ref T, T, T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref ulong location, ulong value, ulong comparand) {
        return TryCompareExchange(ref location, value, comparand, out _);
    }

    /// <inheritdoc cref="TryCompareExchange{T}(ref T, T, T, out T)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCompareExchange(ref ulong location, ulong value, ulong comparand, out ulong current) {
        current = Interlocked.CompareExchange(ref location, value, comparand);
        return current == comparand;
    }

    #endregion

    #region Lazy (Publish-Once) Initialization

    /// <summary>
    /// Atomically publishes <paramref name="value"/> at the specified location if that location is still
    /// <see langword="null"/>, and returns the value that is visible to every thread afterwards.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <param name="location">The location to initialize.</param>
    /// <param name="value">The candidate value to publish.</param>
    /// <returns>
    /// <paramref name="value"/> if this call won the race; otherwise the value another thread had already
    /// published. Either way, the result is the single winning instance.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// No retry loop is needed because the <see langword="null"/>-to-non-<see langword="null"/> transition happens
    /// at most once. If <paramref name="value"/> owns a resource that must be released when this call loses the
    /// race, use <see cref="TryInitialize{T}(ref T, T, out T)"/> instead: the losing instance is otherwise
    /// discarded silently and never disposed.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Initialize<T>(ref T? location, T value) where T : class {
        ArgumentNullException.ThrowIfNull(value);
        return Interlocked.CompareExchange(ref location, value, null) ?? value;
    }

    /// <summary>
    /// Atomically publishes <paramref name="value"/> at the specified location if that location is still
    /// <see langword="null"/>, reporting whether this call was the one that published it.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <param name="location">The location to initialize.</param>
    /// <param name="value">The candidate value to publish.</param>
    /// <param name="current">The winning instance, whether it came from this call or from another thread.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> was published; otherwise <see langword="false"/>, in
    /// which case the caller still owns <paramref name="value"/> and is responsible for disposing it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryInitialize<T>(ref T? location, T value, out T current) where T : class {
        ArgumentNullException.ThrowIfNull(value);
        current = Interlocked.CompareExchange(ref location, value, null) ?? value;
        return ReferenceEquals(current, value);
    }

    /// <summary>
    /// Returns the value published at the specified location, creating and publishing one with
    /// <paramref name="factory"/> if the location is still <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <param name="location">The location to read or initialize.</param>
    /// <param name="factory">The factory invoked only when the location has not been initialized yet.</param>
    /// <returns>The single instance visible at <paramref name="location"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="factory"/> returned <see langword="null"/>.</exception>
    /// <remarks>
    /// The already-initialized path costs a single volatile read, with no interlocked operation and no allocation.
    /// Under contention <paramref name="factory"/> may run on more than one thread and only one result survives,
    /// so the instances it produces must be safe to discard without cleanup.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetOrInitialize<T>(ref T? location, Func<T> factory) where T : class {
        ArgumentNullException.ThrowIfNull(factory);
        return Volatile.Read(ref location) ?? Initialize(ref location, Invoke(factory));
    }

    /// <summary>
    /// Returns the value published at the specified location, creating and publishing one with
    /// <paramref name="factory"/> and caller-supplied state if the location is still <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <typeparam name="TState">The type of the state passed to <paramref name="factory"/>.</typeparam>
    /// <param name="location">The location to read or initialize.</param>
    /// <param name="state">The state forwarded to <paramref name="factory"/>.</param>
    /// <param name="factory">The factory invoked only when the location has not been initialized yet.</param>
    /// <returns>The single instance visible at <paramref name="location"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="factory"/> returned <see langword="null"/>.</exception>
    /// <remarks>
    /// Prefer this overload with a static lambda so that the factory captures nothing and no closure is allocated.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetOrInitialize<T, TState>(ref T? location, TState state, Func<TState, T> factory) where T : class {
        ArgumentNullException.ThrowIfNull(factory);
        return Volatile.Read(ref location) ?? Initialize(ref location, Invoke(factory, state));
    }

    /// <summary>
    /// Atomically publishes <paramref name="value"/> at the specified location if that location is still zero.
    /// </summary>
    /// <param name="location">The location to initialize.</param>
    /// <param name="value">The non-zero value to publish.</param>
    /// <returns>
    /// <paramref name="value"/> if this call won the race; otherwise the non-zero value that was already there.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero.</exception>
    /// <remarks>Zero is reserved as the uninitialized sentinel and therefore cannot be published.</remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Initialize(ref int location, int value) {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        int current = Interlocked.CompareExchange(ref location, value, 0);
        return current == 0 ? value : current;
    }

    /// <summary>
    /// Atomically publishes <paramref name="value"/> at the specified location if that location is still zero,
    /// reporting whether this call was the one that published it.
    /// </summary>
    /// <param name="location">The location to initialize.</param>
    /// <param name="value">The non-zero value to publish.</param>
    /// <param name="current">The winning value, whether it came from this call or from another thread.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> was published; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is zero.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryInitialize(ref int location, int value, out int current) {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        int observed = Interlocked.CompareExchange(ref location, value, 0);
        current = observed == 0 ? value : observed;
        return observed == 0;
    }

    /// <inheritdoc cref="Initialize(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Initialize(ref long location, long value) {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        long current = Interlocked.CompareExchange(ref location, value, 0L);
        return current == 0L ? value : current;
    }

    /// <inheritdoc cref="TryInitialize(ref int, int, out int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryInitialize(ref long location, long value, out long current) {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        long observed = Interlocked.CompareExchange(ref location, value, 0L);
        current = observed == 0L ? value : observed;
        return observed == 0L;
    }

    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T Invoke<T>(Func<T> factory) where T : class {
        T value = factory();
        if(value is null)
            ThrowFactoryReturnedNull();

        return value;
    }

    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T Invoke<T, TState>(Func<TState, T> factory, TState state) where T : class {
        T value = factory(state);
        if(value is null)
            ThrowFactoryReturnedNull();

        return value;
    }

    [DoesNotReturn]
    [DebuggerStepThrough, StackTraceHidden]
    private static void ThrowFactoryReturnedNull() {
        throw new InvalidOperationException("The factory returned null; a lazily initialized value must not be null.");
    }

    #endregion

    #region Lock-Free Compare-And-Swap (CAS) Update Operations

    /// <summary>
    /// Atomically updates the reference at the specified location using a lock-free compare-and-swap loop.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <param name="location">The location to update.</param>
    /// <param name="updateFunction">
    /// A pure, side-effect-free function that maps the current value to the new value. It may be invoked
    /// more than once under contention, so it must not mutate observable state.
    /// </param>
    /// <returns>The value that was successfully installed at <paramref name="location"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="updateFunction"/> is <see langword="null"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    public static T Update<T>(ref T location, Func<T, T> updateFunction) where T : class? {
        ArgumentNullException.ThrowIfNull(updateFunction);

        T initialValue = Volatile.Read(ref location);
        while(true) {
            T newValue = updateFunction(initialValue);
            T currentValue = Interlocked.CompareExchange(ref location, newValue, initialValue);
            if(ReferenceEquals(currentValue, initialValue))
                return newValue;

            initialValue = currentValue;
        }
    }

    /// <summary>
    /// Atomically updates the reference at the specified location using a lock-free compare-and-swap loop,
    /// passing caller-supplied state to the update function to avoid closure allocations.
    /// </summary>
    /// <typeparam name="T">The reference type stored at <paramref name="location"/>.</typeparam>
    /// <typeparam name="TState">The type of the state passed to <paramref name="updateFunction"/>.</typeparam>
    /// <param name="location">The location to update.</param>
    /// <param name="state">The state forwarded to <paramref name="updateFunction"/> on every attempt.</param>
    /// <param name="updateFunction">
    /// A pure, side-effect-free function that maps the current value and <paramref name="state"/> to the new
    /// value. It may be invoked more than once under contention.
    /// </param>
    /// <returns>The value that was successfully installed at <paramref name="location"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="updateFunction"/> is <see langword="null"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    public static T Update<T, TState>(ref T location, TState state, Func<T, TState, T> updateFunction) where T : class? {
        ArgumentNullException.ThrowIfNull(updateFunction);

        T initialValue = Volatile.Read(ref location);
        while(true) {
            T newValue = updateFunction(initialValue, state);
            T currentValue = Interlocked.CompareExchange(ref location, newValue, initialValue);
            if(ReferenceEquals(currentValue, initialValue))
                return newValue;

            initialValue = currentValue;
        }
    }

    /// <inheritdoc cref="Update{T}(ref T, Func{T, T})"/>
    [DebuggerStepThrough, StackTraceHidden]
    public static int Update(ref int location, Func<int, int> updateFunction) {
        ArgumentNullException.ThrowIfNull(updateFunction);

        int initialValue = Volatile.Read(ref location);
        while(true) {
            int newValue = updateFunction(initialValue);
            int currentValue = Interlocked.CompareExchange(ref location, newValue, initialValue);
            if(currentValue == initialValue)
                return newValue;

            initialValue = currentValue;
        }
    }

    /// <inheritdoc cref="Update{T}(ref T, Func{T, T})"/>
    [DebuggerStepThrough, StackTraceHidden]
    public static int Update<TState>(ref int location, TState state, Func<int, TState, int> updateFunction) {
        ArgumentNullException.ThrowIfNull(updateFunction);

        int initialValue = Volatile.Read(ref location);
        while(true) {
            int newValue = updateFunction(initialValue, state);
            int currentValue = Interlocked.CompareExchange(ref location, newValue, initialValue);
            if(currentValue == initialValue)
                return newValue;

            initialValue = currentValue;
        }
    }

    /// <inheritdoc cref="Update{T}(ref T, Func{T, T})"/>
    [DebuggerStepThrough, StackTraceHidden]
    public static long Update(ref long location, Func<long, long> updateFunction) {
        ArgumentNullException.ThrowIfNull(updateFunction);

        long initialValue = Interlocked.Read(ref location);
        while(true) {
            long newValue = updateFunction(initialValue);
            long currentValue = Interlocked.CompareExchange(ref location, newValue, initialValue);
            if(currentValue == initialValue)
                return newValue;

            initialValue = currentValue;
        }
    }

    /// <inheritdoc cref="Update{T}(ref T, Func{T, T})"/>
    [DebuggerStepThrough, StackTraceHidden]
    public static long Update<TState>(ref long location, TState state, Func<long, TState, long> updateFunction) {
        ArgumentNullException.ThrowIfNull(updateFunction);

        long initialValue = Interlocked.Read(ref location);
        while(true) {
            long newValue = updateFunction(initialValue, state);
            long currentValue = Interlocked.CompareExchange(ref location, newValue, initialValue);
            if(currentValue == initialValue)
                return newValue;

            initialValue = currentValue;
        }
    }

    #endregion

    #region Interlocked Numeric Operations

    /// <summary>Atomically increments the value at the specified location.</summary>
    /// <param name="location">The location to increment.</param>
    /// <returns>The incremented value.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Increment(ref int location) {
        return Interlocked.Increment(ref location);
    }

    /// <summary>Atomically decrements the value at the specified location.</summary>
    /// <param name="location">The location to decrement.</param>
    /// <returns>The decremented value.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Decrement(ref int location) {
        return Interlocked.Decrement(ref location);
    }

    /// <summary>Atomically adds <paramref name="value"/> to the value at the specified location.</summary>
    /// <param name="location">The location to add to.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>The new value stored at <paramref name="location"/>.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Add(ref int location, int value) {
        return Interlocked.Add(ref location, value);
    }

    /// <inheritdoc cref="Increment(ref int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Increment(ref uint location) {
        return Interlocked.Increment(ref location);
    }

    /// <inheritdoc cref="Decrement(ref int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Decrement(ref uint location) {
        return Interlocked.Decrement(ref location);
    }

    /// <inheritdoc cref="Add(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Add(ref uint location, uint value) {
        return Interlocked.Add(ref location, value);
    }

    /// <inheritdoc cref="Increment(ref int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Increment(ref long location) {
        return Interlocked.Increment(ref location);
    }

    /// <inheritdoc cref="Decrement(ref int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Decrement(ref long location) {
        return Interlocked.Decrement(ref location);
    }

    /// <inheritdoc cref="Add(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Add(ref long location, long value) {
        return Interlocked.Add(ref location, value);
    }

    /// <inheritdoc cref="Increment(ref int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Increment(ref ulong location) {
        return Interlocked.Increment(ref location);
    }

    /// <inheritdoc cref="Decrement(ref int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Decrement(ref ulong location) {
        return Interlocked.Decrement(ref location);
    }

    /// <inheritdoc cref="Add(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Add(ref ulong location, ulong value) {
        return Interlocked.Add(ref location, value);
    }

    #endregion

    #region Interlocked Bitwise Operations

    /// <summary>
    /// Atomically computes the bitwise AND of the value at the specified location and <paramref name="value"/>,
    /// storing the result at that location.
    /// </summary>
    /// <param name="location">The location to update.</param>
    /// <param name="value">The value to combine with the current value.</param>
    /// <returns>The original value at <paramref name="location"/>.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int And(ref int location, int value) {
        return Interlocked.And(ref location, value);
    }

    /// <summary>
    /// Atomically computes the bitwise OR of the value at the specified location and <paramref name="value"/>,
    /// storing the result at that location.
    /// </summary>
    /// <param name="location">The location to update.</param>
    /// <param name="value">The value to combine with the current value.</param>
    /// <returns>The original value at <paramref name="location"/>.</returns>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Or(ref int location, int value) {
        return Interlocked.Or(ref location, value);
    }

    /// <inheritdoc cref="And(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint And(ref uint location, uint value) {
        return Interlocked.And(ref location, value);
    }

    /// <inheritdoc cref="Or(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Or(ref uint location, uint value) {
        return Interlocked.Or(ref location, value);
    }

    /// <inheritdoc cref="And(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long And(ref long location, long value) {
        return Interlocked.And(ref location, value);
    }

    /// <inheritdoc cref="Or(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Or(ref long location, long value) {
        return Interlocked.Or(ref location, value);
    }

    /// <inheritdoc cref="And(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong And(ref ulong location, ulong value) {
        return Interlocked.And(ref location, value);
    }

    /// <inheritdoc cref="Or(ref int, int)"/>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Or(ref ulong location, ulong value) {
        return Interlocked.Or(ref location, value);
    }

    #endregion
}