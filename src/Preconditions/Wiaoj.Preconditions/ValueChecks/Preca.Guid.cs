namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified GUID is not empty.
    /// </summary>
    /// <param name="argument">The GUID to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> is <see cref="Guid.Empty"/>.</exception>
    /// <remarks>
    /// This method provides optimized validation for GUID values to ensure they contain meaningful identifiers.
    /// Direct comparison with <see cref="Guid.Empty"/> for maximum efficiency.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty(Guid argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument == Guid.Empty) {
            Thrower.ThrowPrecaArgumentException("Guid cannot be empty.", paramName);
        }
    }

    /// <summary>
    /// Validates that the specified GUID is not empty, using a custom exception factory.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The GUID to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is empty, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// This overload enables domain-specific exception handling for empty GUID validation.
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty<TException>(Guid argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        ThrowIfNull(exceptionFactory);

        if (argument == Guid.Empty) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified GUID is not empty, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The GUID to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is empty.</exception>
    /// <remarks>
    /// This overload provides type-safe exception throwing for empty GUID validation.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEmpty<TException>(Guid argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TException : Exception, new() {
        if (argument == Guid.Empty) {
            Thrower.ThrowException<TException>();
        }
    }

    /// <summary>
    /// Validates that the specified nullable GUID is not null or empty.
    /// </summary>
    /// <param name="argument">The nullable GUID to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="argument"/> is null.</exception>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> is <see cref="Guid.Empty"/>.</exception>
    /// <remarks>
    /// This method handles both null and empty validation for nullable GUID values in a single check.
    /// Optimized to check null first, then empty, with minimal allocations.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrEmpty([NotNull, DisallowNull] Guid? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument.HasValue is false) {
            Thrower.ThrowPrecaArgumentNullException(paramName);
        }

        if (argument.Value == Guid.Empty) {
            Thrower.ThrowPrecaArgumentException("Guid cannot be empty.", paramName);
        }
    }

    /// <summary>
    /// Validates that the specified nullable GUID is not null or empty, using a custom exception factory.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The nullable GUID to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is null or empty, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// This overload enables unified exception handling for both null and empty nullable GUID scenarios.
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrEmpty<TException>([NotNull, DisallowNull] Guid? argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        ThrowIfNull(exceptionFactory);

        if (argument.HasValue is false || argument.Value == Guid.Empty) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified nullable GUID is not null or empty, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The nullable GUID to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is null or empty.</exception>
    /// <remarks>
    /// This overload provides type-safe exception throwing for nullable GUID validation, covering both null and empty scenarios.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrEmpty<TException>([NotNull, DisallowNull] Guid? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TException : Exception, new() {
        if (argument.HasValue is false || argument.Value == Guid.Empty) {
            Thrower.ThrowException<TException>();
        }
    }
}