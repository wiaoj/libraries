namespace Wiaoj.Preconditions;

/// <summary>
/// Validation messages for numeric operations and constraints.
/// </summary>
internal static partial class PrecaMessages {
    internal static class Numeric {
        // Zero validation messages
        public const string ValueCannotBeZero = "Value cannot be zero.";
        public const string ValueMustBeZero = "Value must be zero.";

        // Sign validation messages  
        public const string ValueCannotBeNegative = "Value cannot be negative.";
        public const string ValueCannotBePositive = "Value cannot be positive.";
        public const string ValueMustBePositive = "Value must be positive.";
        public const string ValueMustBeNegative = "Value must be negative.";

        // Combined sign and zero validation messages
        public const string ValueCannotBeZeroOrNegative = "Value cannot be zero or negative.";
        public const string ValueCannotBeZeroOrPositive = "Value cannot be zero or positive.";

        // Floating point validation messages
        public const string ValueCannotBeNaN = "Value cannot be NaN.";
        public const string ValueCannotBeInfinity = "Value cannot be infinity.";
        public const string ValueCannotBeNaNOrInfinity = "Value cannot be NaN or infinity.";
        public const string ValueCannotBeSubnormal = "Value cannot be subnormal.";

        // Range validation messages
        public const string ValueOutOfRange = "Value is out of range.";
        public const string ValueTooSmall = "Value is too small.";
        public const string ValueTooLarge = "Value is too large.";

        public const string ValueIsMaxValue = "Value cannot be the maximum value for its type.";
        public const string ValueIsMinValue = "Value cannot be the minimum value for its type.";

        /// <summary>
        /// Creates a dynamic range validation message.
        /// </summary>
        /// <typeparam name="T">The type of the range values.</typeparam>
        /// <param name="min">The minimum allowed value.</param>
        /// <param name="max">The maximum allowed value.</param>
        /// <returns>A formatted range validation message.</returns>
        public static string GetRangeMessage<T>(T min, T max) {
            return $"Value must be between {min} and {max} (inclusive).";
        }

        /// <summary>
        /// Creates a dynamic maximum validation message.
        /// </summary>
        /// <typeparam name="T">The type of the maximum value.</typeparam>
        /// <param name="maximum">The maximum allowed value.</param>
        /// <returns>A formatted maximum validation message.</returns>
        public static string GetMaximumMessage<T>(T maximum) {
            return $"Value must be less than or equal to {maximum}.";
        }

        /// <summary>
        /// Creates a dynamic minimum validation message.
        /// </summary>
        /// <typeparam name="T">The type of the minimum value.</typeparam>
        /// <param name="minimum">The minimum allowed value.</param>
        /// <returns>A formatted minimum validation message.</returns>
        public static string GetMinimumMessage<T>(T minimum) {
            return $"Value must be greater than or equal to {minimum}.";
        }
    }
}