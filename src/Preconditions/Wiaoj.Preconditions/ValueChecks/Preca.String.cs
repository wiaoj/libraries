namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified string is not null or empty.
    /// </summary>
    /// <param name="argument">The string to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="argument"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> is empty. Inherits from <see cref="ArgumentException"/>.</exception>
    /// <remarks>
    /// Use this method to validate string parameters that must contain meaningful content.
    /// More efficient than calling ThrowIfNull and ThrowIfEmpty separately.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrEmpty([NotNull] string? argument,
                                          [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (string.IsNullOrEmpty(argument)) {
            if (argument is null) {
                Thrower.ThrowPrecaArgumentNullException(paramName);
            }

            Thrower.ThrowPrecaArgumentException(PrecaMessages.Text.ValueCannotBeNullOrEmpty, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified string is not null or empty, using a type-safe custom exception factory.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The string to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is null or empty, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// Use this overload when you need type-safe custom exception types or messages for null or empty string validation.
    /// The factory is only invoked if validation fails, ensuring no performance overhead in success cases.
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrEmpty<TException>([NotNull] string? argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (string.IsNullOrEmpty(argument)) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified string is not null or empty, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The string to validate.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is null or empty. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrEmpty<TException>([NotNull] string? argument)
        where TException : Exception, new() {
        if (string.IsNullOrEmpty(argument)) {
            Thrower.ThrowException<TException>();
        }
    }

    /// <summary>
    /// Validates that the specified string is not null, empty, or consists only of whitespace characters.
    /// </summary>
    /// <param name="argument">The string to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="argument"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> is empty or consists only of whitespace. Inherits from <see cref="ArgumentException"/>.</exception>
    /// <remarks>
    /// Use this method to validate string parameters that must contain meaningful content beyond whitespace.
    /// Validates against spaces, tabs, newlines, and other Unicode whitespace characters.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrWhiteSpace([NotNull] string? argument,
                                               [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (string.IsNullOrWhiteSpace(argument)) {
            if (argument is null) {
                Thrower.ThrowPrecaArgumentNullException(paramName);
            }
            else if (argument.Length is Preca.Constants.ZeroInt32) {
                Thrower.ThrowPrecaArgumentException(PrecaMessages.Text.ValueCannotBeNullOrEmpty, paramName);
            }

            Thrower.ThrowPrecaArgumentException(PrecaMessages.Text.ValueCannotBeNullOrWhiteSpace, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified string is not null, empty, or whitespace, using a type-safe custom exception factory.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The string to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is null, empty, or whitespace, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// Use this overload when you need type-safe custom exception types or messages for comprehensive string validation.
    /// The factory is only invoked if validation fails, ensuring no performance overhead in success cases.
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrWhiteSpace<TException>([NotNull] string? argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        if (string.IsNullOrWhiteSpace(argument)) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified string is not null, empty, or whitespace, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The string to validate.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is null, empty, or whitespace. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrWhiteSpace<TException>([NotNull] string? argument)
        where TException : Exception, new() {
        if (string.IsNullOrWhiteSpace(argument)) {
            Thrower.ThrowException<TException>();
        }
    }

    /// <summary>
    /// Throws if the argument contains the specified character.
    /// Optimized using Span to avoid string allocations during check.
    /// </summary>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfContains([NotNull] string? argument, char invalidChar, [CallerArgumentExpression(nameof(argument))] string? paramName = null) {

        if(argument is null) {
            Thrower.ThrowPrecaArgumentNullException(paramName);
        }

        if(argument.Contains(invalidChar)) {
            Thrower.ThrowContains(argument, invalidChar, paramName);
        }
    }
}