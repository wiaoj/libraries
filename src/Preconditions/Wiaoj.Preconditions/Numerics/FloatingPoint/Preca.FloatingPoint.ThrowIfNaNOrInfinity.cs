using System.Numerics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified floating-point value is neither NaN nor infinite.
    /// </summary>
    /// <typeparam name="T">The floating-point type to validate. Must implement <see cref="IFloatingPointIeee754{T}"/>.</typeparam>
    /// <param name="argument">The floating-point value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown when <paramref name="argument"/> is NaN or infinite. Inherits from <see cref="ArgumentOutOfRangeException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure floating-point values are finite, valid numbers.
    /// More efficient than calling ThrowIfNaN and ThrowIfInfinity separately.
    /// Ideal for mathematical calculations that must produce finite results.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNaNOrInfinity<T>(T argument,
                                               [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IFloatingPointIeee754<T> {
        Preca.ThrowIfNull(argument, paramName);

        if (!T.IsFinite(argument)) {
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, argument, PrecaMessages.Numeric.ValueCannotBeNaNOrInfinity);
        }
    }

    /// <summary>
    /// Validates that the specified floating-point value is neither NaN nor infinite, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The floating-point type to validate. Must implement <see cref="IFloatingPointIeee754{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The floating-point value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is NaN or infinite, using the exception from <paramref name="exceptionFactory"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNaNOrInfinity<T, TException>(T argument,
                                               [NotNull] Func<TException> exceptionFactory)
        where T : IFloatingPointIeee754<T>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(exceptionFactory);

        if (!T.IsFinite(argument)) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified floating-point value is neither NaN nor infinite, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The floating-point type to validate. Must implement <see cref="IFloatingPointIeee754{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The floating-point value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is NaN or infinite. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNaNOrInfinity<T, TException>(T argument,
                                                           [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IFloatingPointIeee754<T>
        where TException : Exception, new() {
        Preca.ThrowIfNull(argument, paramName);

        if (!T.IsFinite(argument)) {
            Thrower.ThrowException<TException>();
        }
    }
}