using System.Numerics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified numeric value is within the specified range (inclusive).
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="minimum">The minimum allowed value (inclusive).</param>
    /// <param name="maximum">The maximum allowed value (inclusive).</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown when <paramref name="argument"/> is outside the specified range. Inherits from <see cref="ArgumentOutOfRangeException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure numeric values fall within specified inclusive bounds.
    /// Supports all comparable types including integers, floating-point, and custom comparable types.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfOutOfRange<T>(T argument, T minimum, T maximum,
                                            [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IComparisonOperators<T, T, bool> {
        Preca.ThrowIfNull(argument, paramName);
        Preca.ThrowIfNull(minimum, nameof(minimum));
        Preca.ThrowIfNull(maximum, nameof(maximum));

        if (argument < minimum || argument > maximum) {
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, argument, PrecaMessages.Numeric.GetRangeMessage(minimum, maximum));
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is within the specified range (inclusive), using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="minimum">The minimum allowed value (inclusive).</param>
    /// <param name="maximum">The maximum allowed value (inclusive).</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is outside the specified range, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// This overload enables domain-specific exception handling for range validation.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfOutOfRange<T, TException>(T argument, T minimum, T maximum,
                                            [NotNull] Func<TException> exceptionFactory)
        where T : IComparisonOperators<T, T, bool>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(minimum, nameof(minimum));
        Preca.ThrowIfNull(maximum, nameof(maximum));
        Preca.ThrowIfNull(exceptionFactory);

        if (argument < minimum || argument > maximum) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is within the specified range (inclusive), throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="minimum">The minimum allowed value (inclusive).</param>
    /// <param name="maximum">The maximum allowed value (inclusive).</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is outside the specified range. The specific exception type is determined by the TException generic parameter.</exception>
    /// <remarks>
    /// This overload enables throwing specific exception types while maintaining parameter name information.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfOutOfRange<T, TException>(T argument, T minimum, T maximum,
                                                        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IComparisonOperators<T, T, bool>
        where TException : Exception, new() {
        Preca.ThrowIfNull(argument, paramName);
        Preca.ThrowIfNull(minimum, nameof(minimum));
        Preca.ThrowIfNull(maximum, nameof(maximum));

        if (argument < minimum || argument > maximum) {
            Thrower.ThrowException<TException>();
        }
    }
}