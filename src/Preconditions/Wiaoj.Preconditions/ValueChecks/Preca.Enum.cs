namespace Wiaoj.Preconditions;

public static partial class Preca {
    /// <summary>
    /// Validates that the specified enum value is defined within the enum type.
    /// </summary>
    /// <typeparam name="TEnum">The enum type to validate against. Must be a struct and an enum.</typeparam>
    /// <param name="argument">The enum value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> is not a defined value in the enum type. Inherits from <see cref="ArgumentException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure enum values are valid and defined within their type.
    /// Prevents undefined enum values that could cause unexpected behavior in switch statements or comparisons.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfUndefined<TEnum>(TEnum argument,
                                               [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TEnum : struct, Enum {
        if (Enum.IsDefined(argument) is false) {
            string message = PrecaMessages.Value.GetEnumUndefinedMessage(argument);
            Thrower.ThrowPrecaArgumentException(message, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified enum value is defined within the enum type, using a type-safe custom exception factory.
    /// </summary>
    /// <typeparam name="TEnum">The enum type to validate against. Must be a struct and an enum.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The enum value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is not a defined value in the enum type, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// Use this overload when you need type-safe custom exception types for enum validation.
    /// The factory is only invoked if validation fails, ensuring no performance overhead in success cases.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfUndefined<TEnum, TException>(TEnum argument, [NotNull] Func<TException> exceptionFactory)
        where TEnum : struct, Enum
        where TException : notnull, Exception {
        Preca.ThrowIfNull(exceptionFactory);
        Preca.ThrowIfFalse(Enum.IsDefined(argument), exceptionFactory);
    }

    /// <summary>
    /// Validates that the specified enum value is defined within the enum type, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TEnum">The enum type to validate against. Must be a struct and an enum.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The enum value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> is not a defined value in the enum type. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfUndefined<TEnum, TException>(TEnum argument,
                                                           [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TEnum : struct, Enum
        where TException : Exception, new() {
        Preca.ThrowIfFalse<TException>(Enum.IsDefined(argument), paramName);
    }

    /// <summary>
    /// Validates that the specified flags enum value contains only valid flag combinations.
    /// </summary>
    /// <typeparam name="TEnum">The flags enum type to validate against. Must be a struct, an enum, and marked with FlagsAttribute.</typeparam>
    /// <param name="argument">The flags enum value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> contains invalid flag combinations. Inherits from <see cref="ArgumentException"/>.</exception>
    /// <remarks>
    /// Use this method to ensure flags enum values contain only valid combinations of defined flags.
    /// Prevents invalid flag states that could cause unexpected behavior in bitwise operations.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfInvalidFlags<TEnum>(TEnum argument,
                                                  [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TEnum : struct, Enum {
        if (ValidateAndGetInvalidFlags(argument) is var invalidFlags
            && invalidFlags is not Preca.Constants.ZeroInt64) {
            string message = PrecaMessages.Value.GetEnumInvalidFlagsMessage(argument);
            Thrower.ThrowPrecaArgumentException(message, paramName);
        }
    }

    /// <summary>
    /// Validates that the specified flags enum value contains only valid flag combinations, using a type-safe custom exception factory.
    /// </summary>
    /// <typeparam name="TEnum">The flags enum type to validate against. Must be a struct, an enum, and marked with FlagsAttribute.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must inherit from Exception and be non-null.</typeparam>
    /// <param name="argument">The flags enum value to validate.</param>
    /// <param name="exceptionFactory">A factory function that creates the exception to throw. Cannot be null.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="exceptionFactory"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> contains invalid flag combinations, using the exception from <paramref name="exceptionFactory"/>.</exception>
    /// <remarks>
    /// Use this overload when you need type-safe custom exception types for flags enum validation.
    /// The factory is only invoked if validation fails, ensuring no performance overhead in success cases.
    /// </remarks>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfInvalidFlags<TEnum, TException>(TEnum argument, [NotNull] Func<TException> exceptionFactory)
        where TEnum : struct, Enum
        where TException : notnull, Exception {
        Preca.ThrowIf(
            ValidateAndGetInvalidFlags(argument) is not Preca.Constants.ZeroInt64,
            exceptionFactory);
    }

    /// <summary>
    /// Validates that the specified flags enum value contains only valid flag combinations, throwing a specific exception type.
    /// </summary>
    /// <typeparam name="TEnum">The flags enum type to validate against. Must be a struct, an enum, and marked with FlagsAttribute.</typeparam>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <param name="argument">The flags enum value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="Exception">Thrown when <paramref name="argument"/> contains invalid flag combinations. The specific exception type is determined by the TException generic parameter.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfInvalidFlags<TEnum, TException>(TEnum argument,
                                                              [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TEnum : struct, Enum
        where TException : Exception, new() {
        Preca.ThrowIf<TException>(
            ValidateAndGetInvalidFlags(argument) is not Preca.Constants.ZeroInt64,
            paramName);
    }

    /// <summary>
    /// Helper method to validate flags and return invalid flag bits.
    /// </summary>
    /// <typeparam name="TEnum">The flags enum type.</typeparam>
    /// <param name="argument">The flags enum value to validate.</param>
    /// <returns>Invalid flag bits, or 0 if all flags are valid.</returns>
    /// <exception cref="PrecaArgumentException">Thrown when the enum type is not marked with FlagsAttribute.</exception>
    [DebuggerStepThrough, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ValidateAndGetInvalidFlags<TEnum>(TEnum argument) where TEnum : struct, Enum {
        if (typeof(TEnum).IsDefined(typeof(FlagsAttribute), false) is false) {
            string message = PrecaMessages.Value.GetEnumMustHaveFlagsAttributeMessage<TEnum>();
            Thrower.ThrowPrecaArgumentException(message, nameof(TEnum));
        }

        TEnum[] definedValues = Enum.GetValues<TEnum>();
        long argumentValue = Convert.ToInt64(argument);
        long allValidFlags = Preca.Constants.ZeroInt64;

        foreach (TEnum value in definedValues) {
            allValidFlags |= Convert.ToInt64(value);
        }

        return argumentValue & ~allValidFlags;
    }
}