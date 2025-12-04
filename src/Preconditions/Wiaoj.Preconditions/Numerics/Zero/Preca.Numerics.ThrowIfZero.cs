using System.Numerics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified numeric value is not equal to zero.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="INumberBase{T}"/>.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown when <paramref name="argument"/> equals zero. Inherits from <see cref="ArgumentOutOfRangeException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure numeric values are non-zero.
    /// Commonly used for divisors, scaling factors, and other calculations where zero would be invalid.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfZero<T>(T argument,
                                      [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : INumberBase<T> {
        Preca.ThrowIfNull(argument, paramName);
        if (T.IsZero(argument)) {
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, argument, PrecaMessages.Numeric.ValueCannotBeZero);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is not equal to zero, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="INumberBase{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> equals zero, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfZero<T, TException>(T argument,
                                      [NotNull] Func<TException> exceptionFactory)
        where T : INumberBase<T>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(exceptionFactory);

        if (T.IsZero(argument)) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is not equal to zero, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="INumberBase{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> equals zero. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfZero<T, TException>(T argument,
                                                  [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : INumberBase<T>
        where TException : Exception, new() {
        Preca.ThrowIfNull(argument, paramName);
        if (T.IsZero(argument)) {
            Thrower.ThrowException<TException>();
        }
    }
}