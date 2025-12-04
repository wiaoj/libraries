namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified DateTime has a specified DateTimeKind (not Unspecified).
    /// </summary>
    /// <param name="argument">The DateTime value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> has DateTimeKind.Unspecified. Inherits from <see cref="ArgumentException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure DateTime values have explicit timezone information.
    /// Prevents ambiguous temporal representations in business logic.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfUnspecifiedKind(DateTime argument,
                                              [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument.Kind is DateTimeKind.Unspecified) {
            Thrower.ThrowPrecaArgumentException(PrecaMessages.Value.DateTimeKindCannotBeUnspecified, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified DateTime has a specified DateTimeKind (not Unspecified), using a custom exception factory.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The DateTime value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> has DateTimeKind.Unspecified, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// Use this overload when you need type-safe custom exception types for DateTimeKind validation.
    /// The factory is only invoked if validation fails, ensuring no performance overhead in success cases.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfUnspecifiedKind<TException>(DateTime argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (argument.Kind is DateTimeKind.Unspecified) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified DateTime has a specified DateTimeKind (not Unspecified), throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The DateTime value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> has DateTimeKind.Unspecified. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfUnspecifiedKind<TException>(DateTime argument,
                                                          [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TException : Exception, new() {
        if (argument.Kind is DateTimeKind.Unspecified) {
            Thrower.ThrowException<TException>();
        }
    }
}