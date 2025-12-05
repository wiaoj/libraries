using System.Runtime.CompilerServices;

namespace Wiaoj.Results;
/// <summary>
/// Represents the result of an operation: either a successful value (<typeparamref name="TValue"/>) or a list of errors.
/// This struct is the core primitive for Railway Oriented Programming.
/// </summary>
/// <typeparam name="TValue">The type of the underlying value.</typeparam>
public readonly record struct Result<TValue> : IDisposable {
    private readonly TValue? _value;
    private readonly List<Error>? _errors;

    /// <summary>
    /// Gets a value indicating whether the result represents a failure.
    /// </summary>
    public bool IsError => this._errors is not null;

    /// <summary>
    /// Gets a value indicating whether the result represents a success.
    /// </summary>
    public bool IsSuccess => this._errors is null;

    /// <summary>
    /// Gets the value of the successful operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed when <see cref="IsError"/> is true.</exception>
    public TValue Value => this.IsSuccess
        ? this._value!
        : throw new InvalidOperationException("Cannot access the value of an error result. Use FirstError or Errors instead.");

    /// <summary>
    /// Gets the first error of a failed operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed when <see cref="IsSuccess"/> is true.</exception>
    public Error FirstError => this.IsError
        ? this._errors![0]
        : throw new InvalidOperationException("Cannot access an error of a successful result.");

    /// <summary>
    /// Gets the list of errors. Returns an empty list if the result is successful.
    /// </summary>
    public IReadOnlyList<Error> Errors => this._errors ?? (IReadOnlyList<Error>)[];

    private Result(TValue value) {
        this._value = value;
        this._errors = null;
    }

    private Result(List<Error> errors) {
        if (errors is null || errors.Count == 0) {
            throw new ArgumentException("At least one error is required to create an error state.", nameof(errors));
        }

        this._value = default;
        this._errors = errors;
    }

    // --- Implicit Operators ---

    /// <summary>
    /// Implicitly converts a value to a successful <see cref="Result{TValue}"/>.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    public static implicit operator Result<TValue>(TValue value) {
        return new(value);
    }

    /// <summary>
    /// Implicitly converts a single <see cref="Error"/> to a failed <see cref="Result{TValue}"/>.
    /// </summary>
    /// <param name="error">The error to convert.</param>
    public static implicit operator Result<TValue>(Error error) {
        return new([error]);
    }

    /// <summary>
    /// Implicitly converts a list of <see cref="Error"/> objects to a failed <see cref="Result{TValue}"/>.
    /// </summary>
    /// <param name="errors">The list of errors.</param>
    public static implicit operator Result<TValue>(List<Error> errors) {
        return new(errors);
    }

    /// <summary>
    /// Implicitly converts an array of <see cref="Error"/> objects to a failed <see cref="Result{TValue}"/>.
    /// </summary>
    /// <param name="errors">The array of errors.</param>
    public static implicit operator Result<TValue>(Error[] errors) {
        return new([.. errors]);
    }

    // --- Factory Methods ---

    /// <summary>
    /// Creates a successful result containing the specified value.
    /// </summary>
    /// <param name="value">The success value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue> Success(TValue value) {
        return new(value);
    }

    // --- ROP Methods ---

    /// <summary>
    /// Transforms the result by applying a function to the value if successful, or handling errors if failed.
    /// </summary>
    /// <typeparam name="TResult">The return type of the transformation.</typeparam>
    /// <param name="onValue">The function to execute if the result is successful.</param>
    /// <param name="onError">The function to execute if the result is an error.</param>
    /// <returns>The result of the transformation.</returns>
    public TResult Match<TResult>(Func<TValue, TResult> onValue, Func<IReadOnlyList<Error>, TResult> onError) {
        if (this.IsError) {
            return onError(this.Errors);
        }

        return onValue(this.Value);
    }

    /// <summary>
    /// Executes an action based on the result state (Success or Error).
    /// </summary>
    /// <param name="onValue">The action to execute if successful.</param>
    /// <param name="onError">The action to execute if failed.</param>
    public void Switch(Action<TValue> onValue, Action<IReadOnlyList<Error>> onError) {
        if (this.IsError) {
            onError(this.Errors);
        }
        else {
            onValue(this.Value);
        }
    }

    /// <summary>
    /// Chains another operation if the current result is successful.
    /// If the current result is an error, the error is propagated without executing the next step.
    /// </summary>
    /// <typeparam name="TNextValue">The value type of the next result.</typeparam>
    /// <param name="next">The function to execute if the current result is successful.</param>
    /// <returns>The result of the next operation or the existing errors.</returns>
    public Result<TNextValue> Then<TNextValue>(Func<TValue, Result<TNextValue>> next) {
        if (this.IsError) {
            return this._errors!; // Propagate errors
        }

        return next(this.Value);
    }

    /// <summary>
    /// Disposes the underlying value if it implements <see cref="IDisposable"/>.
    /// </summary>
    public void Dispose() {
        if (this._value is IDisposable disposable) {
            disposable.Dispose();
        }
    }
}