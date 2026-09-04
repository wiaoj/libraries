using System.Numerics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified numeric value is a power of 2 (1, 2, 4, 8, 16, ...).
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="INumberBase{T}"/>.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentValueException">Thrown when <paramref name="argument"/> is not a power of 2. Inherits from <see cref="ArgumentOutOfRangeException"/>.</exception>
    /// <remarks>
    /// A value is a power of 2 if it can be expressed as 2^n for some non-negative integer n (1, 2, 4, 8, etc.).
    /// This method uses <see cref="BitOperations.IsPow2(ulong)"/> for validation after converting the value to ulong.
    /// Commonly used for shard counts, buffer sizes, and other performance-critical allocations.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotPowerOfTwo<T>(T argument,
                                               [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : INumberBase<T> {
        Preca.ThrowIfNull(argument, paramName);

        // Convert to ulong for IsPow2 check
        ulong ulongValue;
        try {
            ulongValue = ulong.CreateSaturating(argument);
        }
        catch {
            Thrower.ThrowPrecaArgumentValueException(paramName, argument, PrecaMessages.Numeric.ValueMustBePowerOfTwo);
            return;
        }

        if(ulongValue == 0 || !BitOperations.IsPow2(ulongValue)) {
            Thrower.ThrowPrecaArgumentValueException(paramName, argument, PrecaMessages.Numeric.ValueMustBePowerOfTwo);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is a power of 2, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="INumberBase{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is not a power of 2, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotPowerOfTwo<T, TException>(T argument,
                                                           [NotNull] Func<TException> exceptionFactory)
        where T : INumberBase<T>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(exceptionFactory);

        // Convert to ulong for IsPow2 check
        ulong ulongValue;
        try {
            ulongValue = ulong.CreateSaturating(argument);
        }
        catch {
            Thrower.ThrowFromFactory(exceptionFactory);
            return;
        }

        if(ulongValue == 0 || !BitOperations.IsPow2(ulongValue)) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }
}