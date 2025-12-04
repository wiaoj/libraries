namespace Wiaoj.Preconditions;

/// <summary>
/// Core validation messages for fundamental Preca operations.
/// </summary>
internal static partial class PrecaMessages {
    internal static class Core {
        /// <summary>
        /// Standard null validation message.
        /// </summary>
        public const string ValueCannotBeNull = "Value cannot be null.";
        public const string ExceptionFactoryReturnedNull = "Exception factory returned null.";

        /// <summary>
        /// Generic conditional validation failure message.
        /// </summary>
        public const string ConditionFailed = "Condition failed.";

        /// <summary>
        /// Creates a dynamic conditional validation message with context.
        /// </summary>
        /// <param name="condition">The condition that failed.</param>
        /// <param name="actualValue">The actual value that caused the failure.</param>
        /// <returns>A formatted conditional validation message.</returns>
        public static string GetConditionalMessage(string condition, object? actualValue) {
            return $"Condition '{condition}' failed. Actual value: {actualValue ?? "null"}.";
        }
    }
}