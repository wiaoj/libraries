namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified argument is not null.
    /// </summary>
    /// <param name="argument">The argument to validate. Must not be null.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="argument"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <remarks>
    /// This method provides high-performance null checking with aggressive inlining.
    /// The parameter name is automatically captured using CallerArgumentExpressionAttribute.
    /// </remarks>
    [DebuggerStepThrough, DebuggerHidden, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument is null) {
            Thrower.ThrowPrecaArgumentNullException(paramName);
        }
    }

    /// <summary>
    /// Validates that the specified argument is not null.
    /// </summary>
    /// <typeparam name="T">The type of the argument to validate.</typeparam>
    /// <param name="argument">The argument to validate. Must not be null.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="argument"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <remarks>
    /// This generic overload provides better type inference and null-state analysis support.
    /// Helps catch null reference issues at compile time with nullable reference types enabled.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull<T>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument is null) {
            Thrower.ThrowPrecaArgumentNullException(paramName);
        }
    }

    /// <summary>
    /// Validates that the specified argument is not null, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The type of the argument to validate.</typeparam> 
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The argument to validate. Must not be null.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is null, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// Use this overload when you need custom exception types or messages.
    /// The factory is only invoked if validation fails, ensuring no performance overhead in success cases.
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull<T, TException>([NotNull] T? argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);

        if (argument is null) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull<T, TState, TException>([NotNull] T? argument,
                                                          [NotNull] Func<TState, TException> exceptionFactory,
                                                          [NotNull] TState state)
       where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        Preca.ThrowIfNull(state);

        if (argument is null) {
            Thrower.ThrowFromFactory(exceptionFactory, state);
        }
    }

    /// <summary>
    /// Validates that the specified argument is not null, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The type of the argument to validate.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The argument to validate. Must not be null.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is null. The specific exception type is determined by the TException generic parameter.</exception>
    /// <remarks>
    /// Use this overload when you need to throw specific exception types for domain-specific error handling.
    /// The exception is created using the parameterless constructor.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull<T, TException>([NotNull] T? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TException : Exception, new() {
        if (argument is null) {
            Thrower.ThrowException<TException>();
        }
    }
}