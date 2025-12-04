namespace Wiaoj.Preconditions;

/// <summary>
/// Validation messages for value types and type operations.
/// </summary>
internal static partial class PrecaMessages {
    internal static class Value {
        // DateTime validation messages
        public const string ValueCannotBeInThePast = "Value cannot be in the past.";
        public const string ValueCannotBeInTheFuture = "Value cannot be in the future.";
        public const string DateTimeKindCannotBeUnspecified = "DateTime.Kind cannot be Unspecified.";

        // TimeSpan validation messages
        public const string TimeSpanCannotBeNegative = "TimeSpan value cannot be negative.";
        public const string TimeSpanCannotBeNegativeOrZero = "TimeSpan value must be positive and cannot be zero or negative.";

        // Boolean validation messages
        public const string ValueCannotBeTrue = "Value cannot be true.";
        public const string ValueCannotBeFalse = "Value cannot be false.";

        // Enum validation messages
        public const string ValueNotDefined = "Value is not a valid enum value.";
        public const string InvalidFlagCombination = "Value contains invalid flag combinations.";
        public const string EnumMustHaveFlagsAttribute = "Enum type must be marked with FlagsAttribute for flags validation.";

        // Type validation messages
        public const string TypeMustBeAssignableFrom = "Type must be assignable from the specified type.";
        public const string TypeCannotBeAbstract = "Type cannot be abstract.";
        public const string TypeCannotBeInterface = "Type cannot be an interface.";

        /// <summary>
        /// Creates a dynamic enum undefined validation message.
        /// </summary>
        /// <typeparam name="TEnum">The enum type.</typeparam>
        /// <param name="value">The invalid enum value.</param>
        /// <returns>A formatted enum validation message.</returns>
        public static string GetEnumUndefinedMessage<TEnum>(TEnum value) where TEnum : struct, Enum {
            return $"Enum value '{value}' is not defined for enum {typeof(TEnum).Name}.";
        }

        /// <summary>
        /// Creates a dynamic enum invalid flags validation message.
        /// </summary>
        /// <typeparam name="TEnum">The enum type.</typeparam>
        /// <param name="value">The invalid flag combination value.</param>
        /// <returns>A formatted enum flags validation message.</returns>
        public static string GetEnumInvalidFlagsMessage<TEnum>(TEnum value) where TEnum : struct, Enum {
            return $"Enum value '{value}' contains invalid flag combinations for {typeof(TEnum).Name}.";
        }

        /// <summary>
        /// Creates a dynamic enum flags attribute validation message.
        /// </summary>
        /// <typeparam name="TEnum">The enum type.</typeparam>
        /// <returns>A formatted enum flags attribute validation message.</returns>
        public static string GetEnumMustHaveFlagsAttributeMessage<TEnum>() where TEnum : struct, Enum {
            return $"Enum type '{typeof(TEnum).Name}' must be marked with FlagsAttribute for flags validation.";
        }
    }
}