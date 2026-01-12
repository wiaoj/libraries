using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results; 
/// <summary>
/// Represents the result of an operation: either a successful value (<typeparamref name="TValue"/>) or a list of errors.
/// This struct is the core primitive for Railway Oriented Programming.
/// </summary>
/// <typeparam name="TValue">The type of the underlying value.</typeparam>
public readonly record struct Result<TValue> : IResult, IDisposable {
    private readonly TValue? _value;
    private readonly List<Error>? _errors;

    /// <summary>
    /// Gets a value indicating whether the result represents a failure.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_errors))]
    [MemberNotNullWhen(false, nameof(_value))] // TValue class ise value null değildir (genel kabul)
    public bool IsError => this._errors is not null;

    /// <summary>
    /// Gets a value indicating whether the result represents a success.
    /// </summary>
    [MemberNotNullWhen(false, nameof(_errors))]
    public bool IsSuccess => this._errors is null;

    /// <summary>
    /// Gets the value of the successful operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed when <see cref="IsError"/> is true.</exception>
    public TValue Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.IsSuccess
            ? this._value!
            : throw new InvalidOperationException("Cannot access the value of an error result. Use FirstError or Errors instead.");
    }

    /// <summary>
    /// Gets the first error of a failed operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed when <see cref="IsSuccess"/> is true.</exception>
    public Error FirstError {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.IsError
            ? this._errors[0]
            : throw new InvalidOperationException("Cannot access an error of a successful result.");
    }

    /// <summary>
    /// Gets the list of errors. Returns an empty list if the result is successful.
    /// </summary>
    public IReadOnlyList<Error> Errors {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._errors ?? (IReadOnlyList<Error>)[];
    }

    private Result(TValue value) {
        this._value = value;
        this._errors = null;
    }

    private Result(List<Error> errors) {
        // Performans: Parametre kontrolünü çağıran yerde yapmak daha iyidir ama safety için burada kalabilir.
        if (errors is null || errors.Count == 0) {
            throw new ArgumentException("At least one error is required to create an error state.", nameof(errors));
        }

        this._value = default;
        this._errors = errors;
    }

    // --- Implicit Operators ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(TValue value) {
        return new(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(Error error) {
        return new([error]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(List<Error> errors) {
        return new(errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(Error[] errors) {
        return new([.. errors]);
    }

    // --- Factory Methods ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue> Success(TValue value) {
        return new(value);
    }

    // --- ROP Core Methods ---

    /// <summary>
    /// Transforms the result by applying a function to the value if successful, or handling errors if failed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Match<TResult>(Func<TValue, TResult> onValue, Func<IReadOnlyList<Error>, TResult> onError) {
        if (this.IsError) {
            return onError(this._errors);
        }
        return onValue(this._value!);
    }

    /// <summary>
    /// Executes an action based on the result state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Switch(Action<TValue> onValue, Action<IReadOnlyList<Error>> onError) {
        if (this.IsError) {
            onError(this._errors);
        }
        else {
            onValue(this._value!);
        }
    }

    // --- Functional Combinators ---

    /// <summary>
    /// Binds the current result to a new result. Useful for chaining operations that may fail.
    /// (Equivalent to "Bind" or "FlatMap").
    /// </summary>
    public Result<TNextValue> Then<TNextValue>(Func<TValue, Result<TNextValue>> next) {
        if (this.IsError) {
            return this._errors;
        }
        return next(this._value);
    }

    /// <summary>
    /// Transforms the successful value into a new value.
    /// Does not allow returning an error (use <see cref="Then{TNextValue}"/> for that).
    /// </summary>
    public Result<TNew> Map<TNew>(Func<TValue, TNew> mapper) {
        if (this.IsError) {
            return this._errors;
        }
        return mapper(this._value);
    }

    /// <summary>
    /// Executes a side-effect (e.g., logging, setting a variable) without changing the result.
    /// </summary>
    public Result<TValue> Do(Action<TValue> action) {
        if (this.IsSuccess) {
            action(this._value);
        }
        return this;
    }

    /// <summary>
    /// Executes a side-effect without accessing the value (e.g. logging).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> Do(Action action) {
        if (this.IsSuccess) {
            action();
        }
        return this;
    }

    /// <summary>
    /// Validates a condition. If the condition is false, returns the specified error.
    /// </summary>
    public Result<TValue> Ensure(Func<TValue, bool> predicate, Error error) {
        if (this.IsError) {
            return this;
        }

        if (!predicate(this._value)) {
            return error;
        }

        return this;
    }

    /// <summary>
    /// Attempts to recover from an error by returning a fallback value.
    /// </summary>
    public Result<TValue> Recover(Func<IReadOnlyList<Error>, TValue> recover) {
        if (this.IsSuccess) {
            return this;
        }
        return recover(this._errors);
    }

    /// <summary>
    /// Executes an action only if the result is successful.
    /// (Alias for Do)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> IfSuccess(Action<TValue> action) {
        return this.Do(action);
    }

    /// <summary>
    /// Executes an action only if the result is a failure.
    /// Useful for logging errors without breaking the chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> IfFailure(Action<IReadOnlyList<Error>> action) {
        if (this.IsError) {
            action(this._errors);
        }
        return this;
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