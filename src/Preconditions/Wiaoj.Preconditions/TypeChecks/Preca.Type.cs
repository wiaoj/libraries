namespace Wiaoj.Preconditions;

public static partial class Preca {
    #region ThrowIfNotType

    /// <summary>
    /// Validates that the specified argument is of the expected type <typeparamref name="TExpected"/>.
    /// </summary>
    /// <typeparam name="TExpected">The expected type of the argument.</typeparam>
    /// <param name="argument">The argument to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This is automatically populated by the compiler.</param>
    /// <exception cref="PrecaInvalidTypeException">Thrown when <paramref name="argument"/> is null or not of type <typeparamref name="TExpected"/>.</exception>
    /// <remarks>
    /// This method uses the 'is not' pattern, which correctly handles null checks and type validation in a single pass.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfNotType<TExpected>(
        object? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if(argument is not TExpected) {
            Thrower.ThrowPrecaInvalidTypeException<TExpected>(argument, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified argument is of the expected type <typeparamref name="TExpected"/>, using a custom exception factory.
    /// </summary>
    /// <typeparam name="TExpected">The expected type of the argument.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from <see cref="Exception"/> and be non-null.</typeparam>
    /// <param name="argument">The argument to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is not of type <typeparamref name="TExpected"/>, using the exception created by <paramref name="exceptionFactory"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfNotType<TExpected, TException>(
        object? argument,
        [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory, nameof(exceptionFactory));

        if(argument is not TExpected) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified argument is of the expected type <typeparamref name="TExpected"/>, 
    /// using a stateful custom exception factory to avoid delegate allocations.
    /// </summary>
    /// <typeparam name="TExpected">The expected type of the argument.</typeparam>
    /// <typeparam name="TState">The type of the state object passed to the exception factory.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from <see cref="Exception"/> and be non-null.</typeparam>
    /// <param name="argument">The argument to validate.</param>
    /// <param name="state">The state data passed directly to <paramref name="exceptionFactory"/>, enabling static delegate caching.</param>
    /// <param name="exceptionFactory">A factory function that accepts the provided state and creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="state"/> or <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is not of type <typeparamref name="TExpected"/>, using the exception created by <paramref name="exceptionFactory"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfNotType<TExpected, TState, TException>(
        object? argument,
        [NotNull] TState state,
        [NotNull] Func<TState, TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(state, nameof(state));
        Preca.ThrowIfNull(exceptionFactory, nameof(exceptionFactory));

        if(argument is not TExpected) {
            Thrower.ThrowFromFactory(state, exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified argument is of the expected type <typeparamref name="TExpected"/>, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TExpected">The expected type of the argument.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The argument to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is not of type <typeparamref name="TExpected"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfNotType<TExpected, TException>(
        object? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TException : Exception, new() {
        if(argument is not TExpected) {
            Thrower.ThrowException<TException>();
        }
    }

    #endregion

    #region ThrowIfGenericTypeIsNot

    /// <summary>
    /// Validates that the generic type parameter <typeparamref name="TActual"/> is exactly <typeparamref name="TExpected"/>.
    /// Useful for restricting generic structs or methods to specific types.
    /// </summary>
    /// <typeparam name="TActual">The generic type parameter provided by the user.</typeparam>
    /// <typeparam name="TExpected">The type that <typeparamref name="TActual"/> is required to be.</typeparam>
    /// <param name="message">Optional custom message for the exception.</param>
    /// <exception cref="PrecaInvalidOperationException">Thrown when <typeparamref name="TActual"/> does not match <typeparamref name="TExpected"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfGenericTypeIsNot<TActual, TExpected>(string? message = null) {
        if(typeof(TActual) != typeof(TExpected)) {
            Thrower.ThrowGenericTypeMismatchException<TActual, TExpected>(message);
        }
    }

    /// <summary>
    /// Validates that the generic type parameter <typeparamref name="TActual"/> is exactly <typeparamref name="TExpected"/>, using a custom exception factory.
    /// </summary>
    /// <typeparam name="TActual">The generic type parameter provided by the user.</typeparam>
    /// <typeparam name="TExpected">The type that <typeparamref name="TActual"/> is required to be.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from <see cref="Exception"/> and be non-null.</typeparam>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <typeparamref name="TActual"/> does not match <typeparamref name="TExpected"/>, using the exception created by <paramref name="exceptionFactory"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfGenericTypeIsNot<TActual, TExpected, TException>(
        [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory, nameof(exceptionFactory));

        if(typeof(TActual) != typeof(TExpected)) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the generic type parameter <typeparamref name="TActual"/> is exactly <typeparamref name="TExpected"/>, 
    /// using a stateful custom exception factory to avoid delegate allocations.
    /// </summary>
    /// <typeparam name="TActual">The generic type parameter provided by the user.</typeparam>
    /// <typeparam name="TExpected">The type that <typeparamref name="TActual"/> is required to be.</typeparam>
    /// <typeparam name="TState">The type of the state object passed to the exception factory.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from <see cref="Exception"/> and be non-null.</typeparam>
    /// <param name="state">The state data passed directly to <paramref name="exceptionFactory"/>, enabling static delegate caching.</param>
    /// <param name="exceptionFactory">A factory function that accepts the provided state and creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="state"/> or <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <typeparamref name="TActual"/> does not match <typeparamref name="TExpected"/>, using the exception created by <paramref name="exceptionFactory"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfGenericTypeIsNot<TActual, TExpected, TState, TException>(
        [NotNull] TState state,
        [NotNull] Func<TState, TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(state, nameof(state));
        Preca.ThrowIfNull(exceptionFactory, nameof(exceptionFactory));

        if(typeof(TActual) != typeof(TExpected)) {
            Thrower.ThrowFromFactory(state, exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the generic type parameter <typeparamref name="TActual"/> is exactly <typeparamref name="TExpected"/>, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TActual">The generic type parameter provided by the user.</typeparam>
    /// <typeparam name="TExpected">The type that <typeparamref name="TActual"/> is required to be.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <exception cref="Exception">Thrown when <typeparamref name="TActual"/> does not match <typeparamref name="TExpected"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    [StackTraceHidden]
    public static void ThrowIfGenericTypeIsNot<TActual, TExpected, TException>()
        where TException : Exception, new() {
        if(typeof(TActual) != typeof(TExpected)) {
            Thrower.ThrowException<TException>();
        }
    }

    #endregion
}