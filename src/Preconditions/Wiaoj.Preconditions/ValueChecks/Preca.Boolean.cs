namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates a condition and throws an exception if the condition is true.
    /// </summary>
    /// <param name="condition">The condition to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="condition"/> is true. Inherits from <see cref="ArgumentException"/>.</exception>
    /// <remarks>
    /// Use this method for general conditional validation when you want to throw an exception if a condition is met.
    /// This is the inverse of typical assertion patterns - it throws when the condition is true.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIf([DoesNotReturnIf(true)] bool condition,
                               [CallerArgumentExpression(nameof(condition))] string? paramName = null) {
        if (condition) {
            Thrower.ThrowPrecaArgumentException(PrecaMessages.Core.ConditionFailed, paramName);
        }
    }

    /// <summary>
    /// Validates a condition and throws an exception if the condition is true, using a type-safe custom exception factory.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="condition">The condition to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="condition"/> is true, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// Use this overload when you need type-safe custom exception types for conditional validation.
    /// The factory is only invoked if validation fails, ensuring no performance overhead in success cases.
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIf<TException>([DoesNotReturnIf(true)] bool condition, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (condition) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIf<TState, TException>([DoesNotReturnIf(true)] bool condition, [NotNull] Func<TState, TException> exceptionFactory, TState state)
       where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (condition) {
            Thrower.ThrowFromFactory(exceptionFactory, state);
        }
    }

    /// <summary>
    /// Validates a condition and throws an exception if the condition is true, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="condition">The condition to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="condition"/> is true. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIf<TException>([DoesNotReturnIf(true)] bool condition,
                                           [CallerArgumentExpression(nameof(condition))] string? paramName = null)
        where TException : Exception, new() {
        if (condition) {
            Thrower.ThrowException<TException>();
        }
    }

    /// <summary>
    /// Validates that the specified boolean value is not true.
    /// </summary>
    /// <param name="argument">The boolean value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> is true. Inherits from <see cref="ArgumentException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure boolean parameters that must be false.
    /// Commonly used for validation flags or conditions that should not be met.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfTrue([DoesNotReturnIf(true)] bool argument,
                                   [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument) {
            Thrower.ThrowPrecaArgumentException(PrecaMessages.Value.ValueCannotBeTrue, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified boolean value is not true, using a type-safe custom exception factory.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The boolean value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is true, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// Use this overload when you need type-safe custom exception types for boolean true validation.
    /// The factory is only invoked if validation fails, ensuring no performance overhead in success cases.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfTrue<TException>([DoesNotReturnIf(true)] bool argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIf(argument, exceptionFactory);
    }

    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfTrue<TState, TException>([DoesNotReturnIf(true)] bool argument, [NotNull] Func<TState, TException> exceptionFactory, TState state)
        where TException : notnull, Exception {
        Preca.ThrowIf(argument, exceptionFactory, state);
    }

    /// <summary>
    /// Validates that the specified boolean value is not true, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The boolean value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is true. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfTrue<TException>([DoesNotReturnIf(true)] bool argument,
                                               [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TException : Exception, new() {
        Preca.ThrowIf<TException>(argument, paramName);
    }

    /// <summary>
    /// Validates that the specified boolean value is not false.
    /// </summary>
    /// <param name="argument">The boolean value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> is false. Inherits from <see cref="ArgumentException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure boolean parameters that must be true.
    /// Commonly used for validation flags or conditions that must be met.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfFalse([DoesNotReturnIf(false)] bool argument,
                                    [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument is false) {
            Thrower.ThrowPrecaArgumentException(PrecaMessages.Value.ValueCannotBeFalse, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified boolean value is not false, using a type-safe custom exception factory.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The boolean value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is false, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// Use this overload when you need type-safe custom exception types for boolean false validation.
    /// The factory is only invoked if validation fails, ensuring no performance overhead in success cases.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfFalse<TException>([DoesNotReturnIf(false)] bool argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIf(argument is false, exceptionFactory);
    }  

    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfFalse<TState, TException>([DoesNotReturnIf(false)] bool argument, [NotNull] Func<TState, TException> exceptionFactory, TState state)
        where TException : notnull, Exception {
        Preca.ThrowIf(argument is false, exceptionFactory, state);
    }

    /// <summary>
    /// Validates that the specified boolean value is not false, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The boolean value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is false. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfFalse<TException>([DoesNotReturnIf(false)] bool argument,
                                                [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TException : Exception, new() {
        Preca.ThrowIf<TException>(argument is false, paramName);
    }
}