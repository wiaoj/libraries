using System.Numerics;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified numeric value is strictly greater than the limit.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="limit">The exclusive lower bound limit.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown when <paramref name="argument"/> is less than or equal to <paramref name="limit"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfLessThanOrEqualTo<T>(T argument, T limit,
                                          [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IComparisonOperators<T, T, bool> {
        Preca.ThrowIfNull(argument, paramName);
        Preca.ThrowIfNull(limit, nameof(limit));

        if (argument <= limit) {
            // Not: PrecaMessages.Numeric içerisinde "Deðer {limit}'ten büyük olmalýdýr" 
            // þeklinde bir mesaj üreten (örn: GetGreaterThanMessage) metot olduðu varsayýlmýþtýr.
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, argument, PrecaMessages.Numeric.GetGreaterThanMessage(limit));
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is strictly greater than the limit, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="limit">The exclusive lower bound limit.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is less than or equal to <paramref name="limit"/>, using the exception from <paramref name="exceptionFactory"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfLessThanOrEqualTo<T, TException>(T argument, T limit,
                                          [NotNull] Func<TException> exceptionFactory)
        where T : IComparisonOperators<T, T, bool>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(limit, nameof(limit));
        Preca.ThrowIfNull(exceptionFactory);

        if (argument <= limit) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is strictly greater than the limit, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="IComparable{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="limit">The exclusive lower bound limit.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is less than or equal to <paramref name="limit"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfLessThanOrEqualTo<T, TException>(T argument, T limit,
                                                      [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IComparisonOperators<T, T, bool>
        where TException : Exception, new() {
        Preca.ThrowIfNull(argument, paramName);
        Preca.ThrowIfNull(limit, nameof(limit));

        if (argument <= limit) {
            Thrower.ThrowException<TException>();
        }
    }
}