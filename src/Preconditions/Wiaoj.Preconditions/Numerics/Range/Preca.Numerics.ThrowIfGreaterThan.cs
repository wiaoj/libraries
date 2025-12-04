using System.Numerics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified numeric value is less than or equal to the maximum allowed value.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="maximum">The maximum allowed value (inclusive).</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown when <paramref name="argument"/> is greater than <paramref name="maximum"/>. Inherits from <see cref="ArgumentOutOfRangeException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure numeric values do not exceed specified upper bounds.
    /// Commonly used for validating array bounds, capacity limits, and maximum thresholds.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfGreaterThan<T>(T argument, T maximum,
                                             [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IComparisonOperators<T, T, bool> {
        Preca.ThrowIfNull(argument, paramName);
        Preca.ThrowIfNull(maximum, nameof(maximum));

        if (argument > maximum) {
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, argument, PrecaMessages.Numeric.GetMaximumMessage(maximum));
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is less than or equal to the maximum allowed value, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="maximum">The maximum allowed value (inclusive).</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is greater than <paramref name="maximum"/>, using the exception from <paramref name="exceptionFactory"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfGreaterThan<T, TException>(T argument, T maximum,
                                             [NotNull] Func<TException> exceptionFactory)
        where T : IComparisonOperators<T, T, bool>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(maximum, nameof(maximum));
        Preca.ThrowIfNull(exceptionFactory);

        if (argument > maximum) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is less than or equal to the maximum allowed value, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="maximum">The maximum allowed value (inclusive).</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is greater than <paramref name="maximum"/>. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfGreaterThan<T, TException>(T argument, T maximum,
                                                         [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IComparisonOperators<T, T, bool>
        where TException : Exception, new() {
        Preca.ThrowIfNull(argument, paramName);
        Preca.ThrowIfNull(maximum, nameof(maximum));

        if (argument > maximum) {
            Thrower.ThrowException<TException>();
        }
    }
}