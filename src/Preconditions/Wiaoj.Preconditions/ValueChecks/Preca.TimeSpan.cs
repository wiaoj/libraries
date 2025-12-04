namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified TimeSpan value is not negative.
    /// </summary>
    /// <param name="argument">The TimeSpan value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown when <paramref name="argument"/> has a negative value. Inherits from <see cref="ArgumentOutOfRangeException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure that values such as timeouts are not negative. A zero value is considered valid.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative(TimeSpan argument,
                                       [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument < TimeSpan.Zero) {
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, PrecaMessages.Value.TimeSpanCannotBeNegative);
        }
    }

    /// <summary>
    /// Validates that the specified TimeSpan value is not negative, throwing a custom exception if the validation fails.
    /// </summary>
    /// <typeparam name="TException">The type of the exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The TimeSpan value to validate.</param>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative<TException>(TimeSpan argument)
        where TException : Exception, new() {
        if (argument < TimeSpan.Zero) {
            Thrower.ThrowException<TException>();
        }
    }

    /// <summary>
    /// Validates that the specified TimeSpan value is not negative, using a custom exception factory if the validation fails.
    /// </summary>
    /// <typeparam name="TException">The type of the exception to throw.</typeparam>
    /// <param name="argument">The TimeSpan value to validate.</param>
    /// <param name="exceptionFactory">The factory function that creates the exception to throw if the validation fails.</param>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative<TException>(TimeSpan argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (argument < TimeSpan.Zero) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified TimeSpan value is strictly positive (not negative or zero).
    /// </summary>
    /// <param name="argument">The TimeSpan value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown if <paramref name="argument"/> is negative or zero.</exception>
    /// <remarks>
    /// Use this method to ensure that duration values are strictly positive.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeOrZero(TimeSpan argument,
                                             [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument <= TimeSpan.Zero) {
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, PrecaMessages.Value.TimeSpanCannotBeNegativeOrZero);
        }
    }

    /// <summary>
    /// Validates that the specified TimeSpan value is strictly positive, throwing a custom exception if the validation fails.
    /// </summary>
    /// <typeparam name="TException">The type of the exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The TimeSpan value to validate.</param>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeOrZero<TException>(TimeSpan argument)
        where TException : Exception, new() {
        if (argument <= TimeSpan.Zero) {
            Thrower.ThrowException<TException>();
        }
    }

    /// <summary>
    /// Validates that the specified TimeSpan value is strictly positive, using a custom exception factory if the validation fails.
    /// </summary>
    /// <typeparam name="TException">The type of the exception to throw.</typeparam>
    /// <param name="argument">The TimeSpan value to validate.</param>
    /// <param name="exceptionFactory">The factory function that creates the exception to throw if the validation fails.</param>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeOrZero<TException>(TimeSpan argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (argument <= TimeSpan.Zero) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }
}