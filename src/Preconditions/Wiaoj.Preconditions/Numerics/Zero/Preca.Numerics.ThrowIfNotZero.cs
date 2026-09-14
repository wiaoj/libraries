using System.Numerics;

namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified numeric value is equal to zero.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="INumberBase{T}"/>.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Thrown when <paramref name="argument"/> is not equal to zero. Inherits from <see cref="ArgumentOutOfRangeException"/>.</exception>
    /// <remarks>
    /// <para>This method ensures numeric values are exactly zero, useful for certain mathematical operations.</para>
    /// <para><strong>Usage:</strong> <c>Preca.ThrowIfNotZero(remainder);</c> - Ensures exact zero value</para>
    /// <para><strong>Modern API:</strong> Uses <see cref="INumberBase{T}.Zero"/> for type-safe zero comparison</para>
    /// <para><strong>Exception:</strong> Throws <see cref="PrecaArgumentOutOfRangeException"/> for compatibility with standard exception handling</para>
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotZero<T>(T argument,
                                         [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : INumberBase<T> {
        Preca.ThrowIfNull(argument, paramName);
        if(!T.IsZero(argument)) {
            Thrower.ThrowPrecaArgumentOutOfRangeException(paramName, argument, PrecaMessages.Numeric.ValueMustBeZero);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is equal to zero, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="INumberBase{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is not equal to zero, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// <para>This overload enables domain-specific exception handling for not-zero validation.</para>
    /// <para><strong>Usage:</strong> <c>Preca.ThrowIfNotZero(remainder, () => new InvalidOperationException("Expected exact division"));</c></para>
    /// <para><strong>Related:</strong> Use when not-zero validation failures require specific business exception types</para>
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotZero<T, TException>(T argument,
                                         [NotNull] Func<TException> exceptionFactory)
        where T : INumberBase<T>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(exceptionFactory, nameof(exceptionFactory));

        if(!T.IsZero(argument)) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is equal to zero, 
    /// using a stateful custom exception factory to avoid delegate allocations.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="INumberBase{T}"/>.</typeparam>
    /// <typeparam name="TState">The type of the state object passed to the exception factory.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from <see cref="Exception"/> and be non-null.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="state">The state data passed directly to <paramref name="exceptionFactory"/>, enabling static delegate caching.</param>
    /// <param name="exceptionFactory">A factory function that accepts the provided state and creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="state"/> or <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is not equal to zero, using the exception created by <paramref name="exceptionFactory"/>.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotZero<T, TState, TException>(
        T argument,
        [NotNull] TState state,
        [NotNull] Func<TState, TException> exceptionFactory)
        where T : INumberBase<T>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(argument, nameof(argument));
        Preca.ThrowIfNull(state, nameof(state));
        Preca.ThrowIfNull(exceptionFactory, nameof(exceptionFactory));

        if(!T.IsZero(argument)) {
            Thrower.ThrowFromFactory(state, exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified numeric value is equal to zero, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="T">The numeric type to validate. Must implement <see cref="INumberBase{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The numeric value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is not equal to zero. The specific exception type is determined by the TException generic parameter.</exception>
    /// <remarks>
    /// <para>This overload enables throwing specific exception types while maintaining parameter name information.</para>
    /// <para><strong>Usage:</strong> <c>Preca.ThrowIfNotZero&lt;int, InvalidOperationException&gt;(remainder);</c></para>
    /// <para><strong>Related:</strong> Use when you need specific exception types for domain-specific error handling</para>
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotZero<T, TException>(T argument,
                                                     [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : INumberBase<T>
        where TException : Exception, new() {
        Preca.ThrowIfNull(argument, paramName);
        if(!T.IsZero(argument)) {
            Thrower.ThrowException<TException>();
        }
    }
}