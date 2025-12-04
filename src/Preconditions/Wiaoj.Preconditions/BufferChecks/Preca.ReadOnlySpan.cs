namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified ReadOnlySpan is not empty.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="span">The read-only span to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="span"/> is empty. Inherits from <see cref="ArgumentException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure read-only span parameters contain meaningful content.
    /// Empty spans indicate invalid buffer states for most operations.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty<T>(in ReadOnlySpan<T> span,
                                       [CallerArgumentExpression(nameof(span))] string? paramName = null) {
        if (span.IsEmpty) {
            Thrower.ThrowPrecaArgumentException(PrecaMessages.Buffer.ReadOnlySpanCannotBeEmpty, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified ReadOnlySpan is not empty, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="span">The read-only span to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="span"/> is empty, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// Use this overload when you need type-safe custom exception types for read-only span validation.
    /// The factory is only invoked if validation fails, ensuring no performance overhead in success cases.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty<T, TException>(in ReadOnlySpan<T> span, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (span.IsEmpty) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified ReadOnlySpan is not empty, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="span">The read-only span to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="span"/> is empty. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty<T, TException>(in ReadOnlySpan<T> span,
                                                   [CallerArgumentExpression(nameof(span))] string? paramName = null)
        where TException : Exception, new() {
        if (span.IsEmpty) {
            Thrower.ThrowException<TException>();
        }
    }

    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmptyOrWhiteSpace(in ReadOnlySpan<char> span, [CallerArgumentExpression(nameof(span))] string? paramName = null) {
        if (span.IsEmpty || span.IsWhiteSpace()) {
            Thrower.ThrowPrecaArgumentException(PrecaMessages.Buffer.ReadOnlySpanCannotBeEmptyOrWhiteSpace, paramName);
        }
    }
}