namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified argument is of the expected type <typeparamref name="TExpected"/>.
    /// </summary>
    /// <param name="argument">The argument to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This is automatically populated by the compiler.</param>
    /// <typeparam name="TExpected">The expected type of the argument.</typeparam>
    /// <exception cref="PrecaInvalidTypeException">Thrown when argument is null or not of type <typeparamref name="TExpected"/>.</exception>
    /// <remarks>
    /// This method uses the 'is not' pattern, which correctly handles null checks and type validation in a single pass.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfNotType<TExpected>(
        object? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if(argument is not TExpected) {
            Thrower.ThrowPrecaInvalidTypeException<TExpected>(argument, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified argument is of the expected type, using a custom exception factory.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfNotType<TExpected, TException>(
        object? argument,
        [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        ThrowIfNull(exceptionFactory, nameof(exceptionFactory));

        if(argument is not TExpected) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the generic type parameter <typeparamref name="TActual"/> is exactly <typeparamref name="TExpected"/>.
    /// Useful for restricting generic structs or methods to specific types.
    /// </summary>
    /// <typeparam name="TActual">The generic type parameter provided by the user.</typeparam>
    /// <typeparam name="TExpected">The type that <typeparamref name="TActual"/> is required to be.</typeparam>
    /// <param name="message">Optional custom message for the exception.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfGenericTypeIsNot<TActual, TExpected>(string? message = null) { 
        if(typeof(TActual) != typeof(TExpected)) {
            Thrower.ThrowGenericTypeMismatchException<TActual, TExpected>(message);
        }
    }

    /// <summary>
    /// Validates that the generic type parameter <typeparamref name="TActual"/> is exactly <typeparamref name="TExpected"/>, using a custom exception factory.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfGenericTypeIsNot<TActual, TExpected, TException>(
        [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {

        ThrowIfNull(exceptionFactory, nameof(exceptionFactory));

        if(typeof(TActual) != typeof(TExpected)) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }
}