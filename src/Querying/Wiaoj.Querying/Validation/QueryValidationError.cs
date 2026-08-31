using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wiaoj.Preconditions;

namespace Wiaoj.Querying;

/// <summary>
/// Represents an individual diagnostic validation error encountered while evaluating a <see cref="QueryRequest"/> against a <see cref="QuerySchema{T}"/>.
/// </summary>
[DebuggerDisplay("[{ErrorCode}] {PropertyName ?? \"$\"}: {Message}")]
[StructLayout(LayoutKind.Auto)]
public readonly record struct QueryValidationError : IEquatable<QueryValidationError> {
    /// <summary>
    /// Represents an empty or uninitialized <see cref="QueryValidationError"/> instance.
    /// </summary>
    public static readonly QueryValidationError Empty = default;

    /// <summary>
    /// Gets the name of the property or parameter that caused the error, or <see langword="null"/> for request-level errors.
    /// </summary>
    public string? PropertyName { get; init; }

    /// <summary>
    /// Gets the categorical error code describing the validation failure.
    /// </summary>
    public QueryValidationErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Gets a descriptive message explaining the reason for the validation failure.
    /// Never <see langword="null"/>, even on uninitialized instances.
    /// </summary>
    public string Message {
        get => field ?? string.Empty;
        init => field = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    /// <summary>
    /// Gets the raw value or expression that failed validation, if applicable.
    /// </summary>
    public string? AttemptedValue { get; init; }

    /// <summary>
    /// Gets a value indicating whether this instance represents an empty or uninitialized state.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(this.Message) && this.ErrorCode == default;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryValidationError"/> struct with default values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryValidationError() {
        this.PropertyName = null;
        this.ErrorCode = default;
        this.Message = string.Empty;
        this.AttemptedValue = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryValidationError"/> struct for a request-level error.
    /// </summary>
    /// <param name="errorCode">The categorical error code.</param>
    /// <param name="message">A descriptive error message.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryValidationError(
        QueryValidationErrorCode errorCode,
        string message) : this(null, errorCode, message, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryValidationError"/> struct for a property error without an attempted value.
    /// </summary>
    /// <param name="propertyName">The name of the target property.</param>
    /// <param name="errorCode">The categorical error code.</param>
    /// <param name="message">A descriptive error message.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryValidationError(
        string? propertyName,
        QueryValidationErrorCode errorCode,
        string message) : this(propertyName, errorCode, message, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryValidationError"/> struct with all components.
    /// </summary>
    /// <param name="propertyName">The name of the target property, or <see langword="null"/> for request-level errors.</param>
    /// <param name="errorCode">The categorical error code.</param>
    /// <param name="message">A descriptive error message.</param>
    /// <param name="attemptedValue">The raw value that caused the validation failure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryValidationError(
        string? propertyName,
        QueryValidationErrorCode errorCode,
        string message,
        string? attemptedValue) {
        Preca.ThrowIfEmptyOrWhiteSpace(message);

        this.PropertyName = string.IsNullOrWhiteSpace(propertyName) ? null : propertyName.Trim();
        this.ErrorCode = errorCode;
        this.Message = message.Trim();
        this.AttemptedValue = attemptedValue;
    }

    /// <inheritdoc/>
    public override string ToString() {
        if(this.IsEmpty) {
            return string.Empty;
        }

        string prop = string.IsNullOrEmpty(this.PropertyName) ? "$" : this.PropertyName;
        return $"[{this.ErrorCode}] {prop}: {this.Message}";
    }
}