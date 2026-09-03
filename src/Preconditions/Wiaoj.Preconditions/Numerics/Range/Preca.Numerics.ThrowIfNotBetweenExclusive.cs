using System.Numerics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified numeric value is strictly within the specified range (exclusive: minimum &lt; value &lt; maximum).
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparisonOperators{TSelf, TOther, TResult}"/>.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="minimum">The exclusive lower bound.</param>
    /// <param name="maximum">The exclusive upper bound.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown when <paramref name="argument"/> is less than or equal to <paramref name="minimum"/>, or greater than or equal to <paramref name="maximum"/>. Inherits from <see cref="ArgumentOutOfRangeException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure numeric values fall strictly between specified bounds without including the boundary endpoints.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotBetweenExclusive<T>(T argument, T minimum, T maximum,
                                                     [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IComparisonOperators<T, T, bool> {
        Preca.ThrowIfNull(argument, paramName);
        Preca.ThrowIfNull(minimum, nameof(minimum));
        Preca.ThrowIfNull(maximum, nameof(maximum));

        if(argument <= minimum || argument >= maximum) {
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, argument, $"Value must be strictly between {minimum} and {maximum} (exclusive).");
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is strictly within the specified range (exclusive), using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparisonOperators{TSelf, TOther, TResult}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="minimum">The exclusive lower bound.</param>
    /// <param name="maximum">The exclusive upper bound.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is outside the exclusive range, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// This overload enables domain-specific exception handling for exclusive range validation.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotBetweenExclusive<T, TException>(T argument, T minimum, T maximum,
                                                                 [NotNull] Func<TException> exceptionFactory)
        where T : IComparisonOperators<T, T, bool>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(minimum, nameof(minimum));
        Preca.ThrowIfNull(maximum, nameof(maximum));
        Preca.ThrowIfNull(exceptionFactory);

        if(argument <= minimum || argument >= maximum) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is strictly within the specified range (exclusive), using a state-based custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparisonOperators{TSelf, TOther, TResult}"/>.</typeparam>
    /// <typeparam name="TState">The type of state to pass to the exception factory.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="minimum">The exclusive lower bound.</param>
    /// <param name="maximum">The exclusive upper bound.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw given the state. Cannot be null.</param>
    /// <param name="state">The state object to pass into the exception factory.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> or <paramref name="state"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is outside the exclusive range.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotBetweenExclusive<T, TState, TException>(T argument, T minimum, T maximum,
                                                                         [NotNull] Func<TState, TException> exceptionFactory,
                                                                         [NotNull] TState state)
        where T : IComparisonOperators<T, T, bool>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(minimum, nameof(minimum));
        Preca.ThrowIfNull(maximum, nameof(maximum));
        Preca.ThrowIfNull(exceptionFactory);
        Preca.ThrowIfNull(state);

        if(argument <= minimum || argument >= maximum) {
            Thrower.ThrowFromFactory(exceptionFactory, state);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is strictly within the specified range (exclusive), throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparisonOperators{TSelf, TOther, TResult}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="minimum">The exclusive lower bound.</param>
    /// <param name="maximum">The exclusive upper bound.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is outside the exclusive range. The specific exception type is determined by the TException generic parameter.</exception>
    /// <remarks>
    /// This overload enables throwing specific exception types while maintaining parameter name information.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotBetweenExclusive<T, TException>(T argument, T minimum, T maximum,
                                                                 [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IComparisonOperators<T, T, bool>
        where TException : Exception, new() {
        Preca.ThrowIfNull(argument, paramName);
        Preca.ThrowIfNull(minimum, nameof(minimum));
        Preca.ThrowIfNull(maximum, nameof(maximum));

        if(argument <= minimum || argument >= maximum) {
            Thrower.ThrowException<TException>();
        }
    }
}