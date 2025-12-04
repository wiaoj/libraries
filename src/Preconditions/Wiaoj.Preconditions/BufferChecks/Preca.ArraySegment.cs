namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified ArraySegment is not empty.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array segment.</typeparam>
    /// <param name="segment">The array segment to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="segment"/> is empty.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty<T>(in ArraySegment<T> segment,
                                       [CallerArgumentExpression(nameof(segment))] string? paramName = null) {
        if (segment.Count is Preca.Constants.ZeroInt32) {
            Thrower.ThrowPrecaArgumentException(PrecaMessages.Buffer.ArraySegmentCannotBeEmpty, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified ArraySegment is not empty, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array segment.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="segment">The array segment to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="segment"/> is empty, using the exception from <paramref name="exceptionFactory"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty<T, TException>(in ArraySegment<T> segment, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (segment.Count is Preca.Constants.ZeroInt32) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified ArraySegment is not empty, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array segment.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="segment">The array segment to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="segment"/> is empty. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty<T, TException>(in ArraySegment<T> segment,
                                                   [CallerArgumentExpression(nameof(segment))] string? paramName = null)
        where TException : Exception, new() {
        if (segment.Count is Preca.Constants.ZeroInt32) {
            Thrower.ThrowException<TException>();
        }
    }
}