using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results;

/// <summary>
/// Represents the result of an operation: either a successful value (`TValue`) or one or more errors (`Error`).
/// This serves as a type-safe, functional alternative to exceptions for error handling.
/// </summary>
public readonly record struct ErrorOr<TValue> : IDisposable {
    private readonly TValue? _value;
    private readonly IReadOnlyList<Error>? _errors;

    /// <summary>
    /// Gets a value indicating whether the result is an error.
    /// </summary>
    public bool IsError => this._errors is not null;
    public bool IsSuccess => this._errors is null;

    /// <summary>
    /// Gets the value of the successful operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when attempting to access the value of an error result.</exception>
    public TValue Value => this.IsError
        ? throw new InvalidOperationException("Cannot access the value of an error result. Use FirstError or Errors instead.")
        : this._value!;

    /// <summary>
    /// Gets the first error of a failed operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when attempting to access an error of a successful result.</exception>
    public Error FirstError => !this.IsError
        ? throw new InvalidOperationException("Cannot access an error of a successful result.")
        : this._errors![0];

    /// <summary>
    /// Gets a read-only list of all errors for a failed operation. Returns an empty list for a successful result.
    /// </summary>
    public IReadOnlyList<Error> Errors => this._errors ?? [];

    private ErrorOr(TValue value) {
        this._value = value;
        this._errors = null;
    }

    private ErrorOr(IReadOnlyList<Error> errors) {
        if (errors is null || errors.Count == 0)
            throw new ArgumentException("At least one error is required to create an error state.", nameof(errors));

        this._value = default;
        this._errors = errors;
    }

    // --- Factory Methods & Conversions ---
    public static implicit operator ErrorOr<TValue>(TValue value) {
        return new(value);
    }

    public static implicit operator ErrorOr<TValue>(Error error) {
        return new([error]);
    }

    public static implicit operator ErrorOr<TValue>(List<Error> errors) {
        return new(errors);
    }

    public static implicit operator ErrorOr<TValue>(Error[] errors) {
        return new(errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ErrorOr<TValue> Success(TValue value) {
        return new(value);
    }

    // --- Result Processing ---

    /// <summary>
    /// Transforms the result by applying one of two functions, depending on the state (success or error).
    /// </summary>
    /// <param name="onValue">The function to apply if the result is successful.</param>
    /// <param name="onError">The function to apply if the result is an error.</param>
    /// <returns>The result of applying either the <paramref name="onValue"/> or <paramref name="onError"/> function.</returns>
    public TResult Match<TResult>(Func<TValue, TResult> onValue, Func<IReadOnlyList<Error>, TResult> onError) {
        return this.IsError ? onError(this.Errors) : onValue(this.Value);
    }

    /// <summary>
    /// Executes one of two actions, depending on the state (success or error).
    /// </summary>
    /// <param name="onValue">The action to execute if the result is successful.</param>
    /// <param name="onError">The action to execute if the result is an error.</param>
    public void Switch(Action<TValue> onValue, Action<IReadOnlyList<Error>> onError) {
        if (this.IsError) { onError(this.Errors); } else { onValue(this.Value); }
    }

    // --- Fluent & LINQ-like Methods ---

    /// <summary>
    /// Chains the next operation if the result is successful; otherwise, propagates the error.
    /// This method embodies the Railway-Oriented Programming paradigm.
    /// </summary>
    /// <param name="next">The function that takes the successful value and returns a new ErrorOr result.</param>
    /// <returns>The result of the <paramref name="next"/> function if the current result is successful; otherwise, a new ErrorOr with the existing errors.</returns>
    public ErrorOr<TNextValue> Then<TNextValue>(Func<TValue, ErrorOr<TNextValue>> next) {
        return this.IsError ? new ErrorOr<TNextValue>(this.Errors) : next(this.Value);
    }

    /// <summary>
    /// Executes a side-effect action on a successful value without modifying the result.
    /// </summary>
    /// <param name="action">The action to execute with the successful value.</param>
    /// <returns>The original <see cref="ErrorOr{TValue}"/> instance.</returns>
    public ErrorOr<TValue> Tap(Action<TValue> action) {
        if (!this.IsError) { action(this.Value); }
        return this;
    }

    /// <summary>
    /// Executes a side-effect action on an error result without modifying it. Useful for logging.
    /// </summary>
    /// <param name="action">The action to execute with the list of errors.</param>
    /// <returns>The original <see cref="ErrorOr{TValue}"/> instance.</returns>
    public ErrorOr<TValue> TapError(Action<IReadOnlyList<Error>> action) {
        if (this.IsError) { action(this.Errors); }
        return this;
    }

    /// <summary>
    /// Transforms the successful value using a selector function. Preserves the error state. (LINQ.Select)
    /// </summary>
    public ErrorOr<TResult> Select<TResult>(Func<TValue, TResult> selector) {
        return this.IsError ? new ErrorOr<TResult>(this.Errors) : selector(this.Value);
    }

    /// <summary>
    /// Converts the result to an error if the successful value does not meet a specified condition. (LINQ.Where)
    /// </summary>
    public ErrorOr<TValue> Where(Func<TValue, bool> predicate, Error error) {
        if (this.IsError) return this;
        return predicate(this.Value) ? this : error;
    }

    /// <summary>
    /// Returns the successful value or a fallback value if the result is an error.
    /// </summary>
    public TValue OrElse(TValue fallbackValue) {
        return this.IsError ? fallbackValue : this.Value;
    }

    /// <summary>
    /// Returns the successful value or creates a fallback value using the errors if the result is an error.
    /// </summary>
    public TValue OrElse(Func<IReadOnlyList<Error>, TValue> fallbackFactory) {
        return this.IsError ? fallbackFactory(this.Errors) : this.Value;
    }

    /// <summary>
    /// Combines multiple ErrorOr results. If all are successful, returns a list of their values.
    /// If any result is an error, it collects all errors from all failed results and returns a single error result.
    /// </summary>
    /// <param name="results">A collection of ErrorOr results to combine.</param>
    /// <returns>
    /// An ErrorOr containing a list of all successful values, or an ErrorOr containing a list of all errors.
    /// </returns>
    public static ErrorOr<IReadOnlyList<TValue>> Combine(params ErrorOr<TValue>[] results) {
        if (results.Any(r => r.IsError)) {
            return results.SelectMany(r => r.Errors).ToList();
        }
        return results.Select(r => r.Value).ToList();
    }

    public void Dispose() {
        if (this._value is IDisposable disposable) {
            disposable.Dispose();
        }
    }
}