using System.Numerics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified numeric value is greater than or equal to zero.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="ISignedNumber{T}"/>.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown when <paramref name="argument"/> is less than zero. Inherits from <see cref="ArgumentOutOfRangeException"/>.</exception>
    /// <remarks>
    /// <para>This method ensures numeric values are non-negative, allowing zero but preventing negative values.</para>
    /// <para><strong>Usage:</strong> <c>Preca.ThrowIfNegative(balance);</c> - Ensures non-negative value</para>
    /// <para><strong>Modern API:</strong> Uses efficient <see cref="ISignedNumber{T}"/> static methods for type-safe validation</para>
    /// <para><strong>IEEE 754 Compliance:</strong> Negative zero (-0.0) is treated as zero (non-negative), NaN is treated as neither positive nor negative</para>
    /// <para><strong>Exception:</strong> Throws <see cref="PrecaArgumentOutOfRangeException"/> for compatibility with standard exception handling</para>
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative<T>(T argument,
                                          [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : ISignedNumber<T> {
        Preca.ThrowIfNull(argument, paramName);

        if (T.IsNegative(argument) && !T.IsZero(argument) && !T.IsNaN(argument)) {
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, argument, PrecaMessages.Numeric.ValueCannotBeNegative);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is greater than or equal to zero, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="ISignedNumber{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is less than zero, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// <para>This overload enables domain-specific exception handling for negative value validation.</para>
    /// <para><strong>Usage:</strong> <c>Preca.ThrowIfNegative(balance, () => new InsufficientFundsException("Balance cannot be negative"));</c></para>
    /// <para><strong>Related:</strong> Use when negative value validation failures require specific business exception types</para>
    /// <para>If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.</para>
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative<T, TException>(T argument,
                                          [NotNull] Func<TException> exceptionFactory)
        where T : ISignedNumber<T>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(exceptionFactory);

        if (T.IsNegative(argument) && !T.IsZero(argument) && !T.IsNaN(argument)) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is greater than or equal to zero, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="ISignedNumber{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is less than zero. The specific exception type is determined by the TException generic parameter.</exception>
    /// <remarks>
    /// <para>This overload enables throwing specific exception types while maintaining parameter name information.</para>
    /// <para><strong>Usage:</strong> <c>Preca.ThrowIfNegative&lt;decimal, InvalidOperationException&gt;(balance);</c></para>
    /// <para><strong>Related:</strong> Use when you need specific exception types for domain-specific error handling</para>
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative<T, TException>(T argument,
                                                      [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : ISignedNumber<T>
        where TException : Exception, new() {
        Preca.ThrowIfNull(argument, paramName);

        if (T.IsNegative(argument) && !T.IsZero(argument) && !T.IsNaN(argument)) {
            Thrower.ThrowException<TException>();
        }
    }
}