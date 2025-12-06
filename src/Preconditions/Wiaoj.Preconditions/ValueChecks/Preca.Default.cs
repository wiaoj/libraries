namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified value type is not equal to its default value.
    /// Uses <see cref="IEquatable{T}"/> for optimal performance.
    /// </summary>
    /// <typeparam name="T">The value type to validate. Must implement <see cref="IEquatable{T}"/>.</typeparam>
    /// <param name="argument">The value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> equals its default value.</exception>
    /// <remarks>
    /// This method ensures value types contain meaningful data rather than uninitialized default values.
    /// Uses <see cref="IEquatable{T}.Equals(T)"/> for efficient comparison.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDefault<T>(T argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null) where T : IEquatable<T> {
        if (argument.Equals(default)) {
            Thrower.ThrowPrecaArgumentException($"Value cannot be default({typeof(T).Name}).", paramName);
        }
    }

    /// <summary>
    /// Validates that the specified <see cref="DateTime"/> is not equal to its default value.
    /// Provides specific error messaging for DateTime validation.
    /// </summary>
    /// <param name="argument">The DateTime to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> equals <c>default(DateTime)</c> (01/01/0001).</exception>
    /// <remarks>
    /// This method prevents the use of uninitialized DateTime values which can cause business logic errors.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDefault(DateTime argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument == default) {
            Thrower.ThrowPrecaArgumentException("DateTime cannot be default (01/01/0001).", paramName);
        }
    }

    /// <summary>
    /// Validates that the specified <see cref="DateTimeOffset"/> is not equal to its default value.
    /// Provides specific error messaging for DateTimeOffset validation.
    /// </summary>
    /// <param name="argument">The DateTimeOffset to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> equals <c>default(DateTimeOffset)</c> (01/01/0001).</exception>
    /// <remarks>
    /// This method ensures DateTimeOffset values contain valid timestamps with timezone information.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDefault(DateTimeOffset argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument == default) {
            Thrower.ThrowPrecaArgumentException("DateTimeOffset cannot be default (01/01/0001).", paramName);
        }
    }

    /// <summary>
    /// Validates that the specified <see cref="DateTimeOffset"/> is not equal to its default value, throwing a specified exception type on failure.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from <see cref="Exception"/> and have a parameterless constructor.</typeparam>
    /// <param name="argument">The <see cref="DateTimeOffset"/> to validate.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> equals <c>default(DateTimeOffset)</c>.</exception>
    /// <remarks>
    /// This method provides a simple way to throw a custom exception type for default <see cref="DateTimeOffset"/> values.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDefault<TException>(DateTimeOffset argument) where TException : Exception, new() {
        if (argument == default) {
            Thrower.ThrowException<TException>();
        }
    }

    /// <summary>
    /// Validates that the specified <see cref="TimeSpan"/> is not equal to its default value.
    /// Provides specific error messaging for TimeSpan validation.
    /// </summary>
    /// <param name="argument">The TimeSpan to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> equals <c>default(TimeSpan)</c> (00:00:00).</exception>
    /// <remarks>
    /// This method prevents the use of zero TimeSpan values when meaningful durations are required.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDefault(TimeSpan argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (argument == default) {
            Thrower.ThrowPrecaArgumentException("TimeSpan cannot be default (00:00:00).", paramName);
        }
    }

    /// <summary>
    /// Validates that the specified value type is not equal to its default value, using a custom exception factory.
    /// </summary>
    /// <typeparam name="T">The value type to validate. Must implement <see cref="IEquatable{T}"/>.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> equals its default value, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// This overload enables domain-specific exception handling for default value validation.
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDefault<T, TException>(T argument, [NotNull] Func<TException> exceptionFactory)
        where T : IEquatable<T>
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);

        if (argument.Equals(default)) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified <see cref="DateTime"/> is not equal to its default value, using a custom exception factory.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The DateTime to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> equals <c>default(DateTime)</c>, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// This overload enables custom exception handling for default DateTime validation.
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDefault<TException>(DateTime argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);

        if (argument == default) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified <see cref="DateTimeOffset"/> is not equal to its default value, using a custom exception factory.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The DateTimeOffset to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> equals <c>default(DateTimeOffset)</c>, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// This overload enables custom exception handling for default DateTimeOffset validation.
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDefault<TException>(DateTimeOffset argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);

        if (argument == default) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }

    /// <summary>
    /// Validates that the specified <see cref="TimeSpan"/> is not equal to its default value, using a custom exception factory.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The TimeSpan to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> equals <c>default(TimeSpan)</c>, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// This overload enables custom exception handling for default TimeSpan validation.
    /// If the factory returns null, a PrecaArgumentNullException will be thrown instead to prevent null reference exceptions.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDefault<TException>(TimeSpan argument, [NotNull] Func<TException> exceptionFactory)
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);

        if (argument == default) {
            Thrower.ThrowFromFactory(exceptionFactory);
        }
    }
}