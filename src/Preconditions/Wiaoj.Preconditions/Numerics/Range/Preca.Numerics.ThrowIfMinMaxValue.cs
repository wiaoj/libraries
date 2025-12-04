using System.Numerics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified value is not the maximum value for its type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IMinMaxValue{T}"/> and <see cref="IEqualityOperators{TSelf, TOther, TResult}"/>.</typeparam>
    /// <param name="argument">The value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentValueException">Thrown when <paramref name="argument"/> is equal to <c>T.MaxValue</c>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfMaxValue<T>(T argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IMinMaxValue<T>, IEqualityOperators<T, T, bool> {
        if (argument == T.MaxValue) {
            Thrower.ThrowPrecaArgumentValueException(paramName, argument, PrecaMessages.Numeric.ValueIsMaxValue);
        }
    }

    /// <summary>
    /// Validates that the specified value is not the maximum value for its type, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate.</typeparam>
    /// <typeparam name="TException">The type of exception to throw.</typeparam>
    /// <param name="argument">The value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw.</param>
    /// <exception cref="Exception">A custom exception thrown when the validation fails.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfMaxValue<T, TException>(T argument, [NotNull] Func<TException> exceptionFactory)
        where T : IMinMaxValue<T>, IEqualityOperators<T, T, bool>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (argument == T.MaxValue) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified value is not the maximum value for its type, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The value to validate.</param>
    /// <exception cref="Exception">An instance of <typeparamref name="TException"/> thrown when the validation fails.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfMaxValue<T, TException>(T argument)
        where T : IMinMaxValue<T>, IEqualityOperators<T, T, bool>
        where TException : Exception, new() {
        if (argument == T.MaxValue) {
            Thrower.ThrowException<TException>();
        }
    }

    /// <summary>
    /// Validates that the specified value is not the minimum value for its type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IMinMaxValue{T}"/> and <see cref="IEqualityOperators{TSelf, TOther, TResult}"/>.</typeparam>
    /// <param name="argument">The value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentValueException">Thrown when <paramref name="argument"/> is equal to <c>T.MinValue</c>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfMinValue<T>(T argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IMinMaxValue<T>, IEqualityOperators<T, T, bool> {
        if (argument == T.MinValue) {
            Thrower.ThrowPrecaArgumentValueException(paramName, argument, PrecaMessages.Numeric.ValueIsMinValue);
        }
    }

    /// <summary>
    /// Validates that the specified value is not the minimum value for its type, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate.</typeparam>
    /// <typeparam name="TException">The type of exception to throw.</typeparam>
    /// <param name="argument">The value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw.</param>
    /// <exception cref="Exception">A custom exception thrown when the validation fails.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfMinValue<T, TException>(T argument, [NotNull] Func<TException> exceptionFactory)
        where T : IMinMaxValue<T>, IEqualityOperators<T, T, bool>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (argument == T.MinValue) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified value is not the minimum value for its type, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The value to validate.</param>
    /// <exception cref="Exception">An instance of <typeparamref name="TException"/> thrown when the validation fails.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfMinValue<T, TException>(T argument)
        where T : IMinMaxValue<T>, IEqualityOperators<T, T, bool>
        where TException : Exception, new() {
        if (argument == T.MinValue) {
            Thrower.ThrowException<TException>();
        }
    }
}