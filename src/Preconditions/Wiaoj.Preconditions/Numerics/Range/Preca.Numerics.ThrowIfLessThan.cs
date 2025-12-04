using System.Numerics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified numeric value is greater than or equal to the minimum allowed value.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="minimum">The minimum allowed value (inclusive).</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown when <paramref name="argument"/> is less than <paramref name="minimum"/>. Inherits from <see cref="ArgumentOutOfRangeException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure numeric values meet specified lower bounds.
    /// Commonly used for validating array indices, count parameters, and minimum thresholds.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfLessThan<T>(T argument, T minimum,
                                          [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IComparisonOperators<T, T, bool> {
        Preca.ThrowIfNull(argument, paramName);
        Preca.ThrowIfNull(minimum, nameof(minimum));

        if (argument < minimum) {
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, argument, PrecaMessages.Numeric.GetMinimumMessage(minimum));
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is greater than or equal to the minimum allowed value, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="minimum">The minimum allowed value (inclusive).</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is less than <paramref name="minimum"/>, using the exception from <paramref name="exceptionFactory"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfLessThan<T, TException>(T argument, T minimum,
                                          [NotNull] Func<TException> exceptionFactory)
        where T : IComparisonOperators<T, T, bool>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(minimum, nameof(minimum));
        Preca.ThrowIfNull(exceptionFactory);

        if (argument < minimum) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is greater than or equal to the minimum allowed value, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="minimum">The minimum allowed value (inclusive).</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is less than <paramref name="minimum"/>. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfLessThan<T, TException>(T argument, T minimum,
                                                      [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IComparisonOperators<T, T, bool>
        where TException : Exception, new() {
        Preca.ThrowIfNull(argument, paramName);
        Preca.ThrowIfNull(minimum, nameof(minimum));

        if (argument < minimum) {
            Thrower.ThrowException<TException>();
        }
    }
}