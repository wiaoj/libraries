using System.Numerics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified floating-point value is not subnormal (denormalized).
    /// </summary>
    /// <typeparam name="T">The floating-point type to validate. Must implement <see cref="IFloatingPointIeee754{T}"/>.</typeparam>
    /// <param name="argument">The floating-point value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown when <paramref name="argument"/> is subnormal. Inherits from <see cref="ArgumentOutOfRangeException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure floating-point values are normalized numbers.
    /// Prevents subnormal values that can cause precision loss and performance degradation.
    /// Supports all IEEE 754 floating-point types including double, float, and Half.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfSubnormal<T>(T argument,
                                           [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IFloatingPointIeee754<T> {
        Preca.ThrowIfNull(argument, paramName);

        if (T.IsSubnormal(argument)) {
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, argument, PrecaMessages.Numeric.ValueCannotBeSubnormal);
        }
    }

    /// <summary>
    /// Validates that the specified floating-point value is not subnormal, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The floating-point type to validate. Must implement <see cref="IFloatingPointIeee754{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The floating-point value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is subnormal, using the exception from <paramref name="exceptionFactory"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfSubnormal<T, TException>(T argument,
                                           [NotNull] Func<TException> exceptionFactory)
        where T : IFloatingPointIeee754<T>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(exceptionFactory);

        if (T.IsSubnormal(argument)) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified floating-point value is not subnormal, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The floating-point type to validate. Must implement <see cref="IFloatingPointIeee754{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The floating-point value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is subnormal. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfSubnormal<T, TException>(T argument,
                                                       [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : IFloatingPointIeee754<T>
        where TException : Exception, new() {
        Preca.ThrowIfNull(argument, paramName);

        if (T.IsSubnormal(argument)) {
            Thrower.ThrowException<TException>();
        }
    }
}