namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified ReadOnlyMemory is not empty.
    /// </summary>
    /// <typeparam name="T">The type of elements in the memory.</typeparam>
    /// <param name="memory">The read-only memory to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="memory"/> is empty. Inherits from <see cref="ArgumentException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure read-only memory parameters contain meaningful content.
    /// Empty memory regions indicate invalid buffer states for most operations.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty<T>(in ReadOnlyMemory<T> memory, [CallerArgumentExpression(nameof(memory))] string? paramName = null) {
        if (memory.IsEmpty) {
            Thrower.ThrowPrecaArgumentException(PrecaMessages.Buffer.ReadOnlyMemoryCannotBeEmpty, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified ReadOnlyMemory is not empty, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The type of elements in the memory.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="memory">The read-only memory to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="memory"/> is empty, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// Use this overload when you need type-safe custom exception types for read-only memory validation.
    /// The factory is only invoked if validation fails, ensuring no performance overhead in success cases.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty<T, TException>(in ReadOnlyMemory<T> memory, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (memory.IsEmpty) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified ReadOnlyMemory is not empty, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The type of elements in the memory.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="memory">The read-only memory to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="memory"/> is empty. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty<T, TException>(in ReadOnlyMemory<T> memory, [CallerArgumentExpression(nameof(memory))] string? paramName = null)
        where TException : Exception, new() {
        if (memory.IsEmpty) {
            Thrower.ThrowException<TException>();
        }
    }
}