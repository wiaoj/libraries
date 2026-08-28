using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results;

/// <summary>
/// Represents the result of an operation: either a successful value (<typeparamref name="TValue"/>)
/// or a non-empty list of <see cref="Error"/>s.
/// </summary>
/// <typeparam name="TValue">The type of the underlying success value.</typeparam>
public readonly record struct Result<TValue> : IResult {
    private readonly TValue? _value;
    private readonly Error _singleError;
    private readonly List<Error>? _multipleErrors;
    private readonly bool _isSuccess;

    /// <summary>
    /// Gets a value indicating whether the result represents a failure.
    /// </summary>
    public bool IsFailure {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !this._isSuccess;
    }

    /// <summary>
    /// Gets a value indicating whether the result represents a success.
    /// </summary>
    public bool IsSuccess {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._isSuccess;
    }

    /// <summary>
    /// Gets the success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="IsFailure"/> is <see langword="true"/> or the result is uninitialized.
    /// </exception>
    public TValue Value {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            if(!this._isSuccess)
                ThrowValueOnFailureException();

            return this._value!;
        }
    }

    /// <summary>
    /// Gets the first error of a failed result.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="IsSuccess"/> is <see langword="true"/> or the result is in an uninitialized default state.
    /// </exception>
    public Error FirstError {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            if(this._isSuccess)
                ThrowErrorOnSuccessException();

            if(this._multipleErrors is not null)
                return this._multipleErrors[0];

            if(!this._singleError.Equals(default))
                return this._singleError;

            return Error.Uninitialized;
        }
    }

    /// <summary>
    /// Gets the list of errors. Returns an empty collection when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result is in an uninitialized default state.
    /// </exception>
    public IReadOnlyList<Error> Errors {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            if(this._isSuccess)
                return [];

            if(this._multipleErrors is not null)
                return this._multipleErrors;

            if(!this._singleError.Equals(default))
                return [this._singleError];
             
            return [Error.Uninitialized];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Result(TValue value) {
        this._isSuccess = true;
        this._value = value;
        this._singleError = default;
        this._multipleErrors = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Result(Error error) {
        this._isSuccess = false;
        this._value = default;
        this._singleError = error;
        this._multipleErrors = null;
    }

    private Result(params List<Error> errors) {
        if(errors is null || errors.Count == 0)
            ThrowEmptyErrorListException();

        this._isSuccess = false;
        this._value = default;

        if(errors.Count == 1) {
            this._singleError = errors[0];
            this._multipleErrors = null;
        }
        else {
            this._singleError = default;
            this._multipleErrors = errors;
        }
    }

    // ── Pattern Matching & TryGetters ─────────────────────────────────────────

    /// <summary>
    /// Attempts to extract the success value if available.
    /// </summary>
    /// <param name="value">The extracted value when successful, or default when failed.</param>
    /// <returns><see langword="true"/> if the result is successful; otherwise, <see langword="false"/>.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue([MaybeNullWhen(false)] out TValue value) {
        if(this._isSuccess) {
            value = this._value!;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Attempts to extract the errors if the result represents a failure.
    /// </summary>
    /// <param name="errors">The list of errors when failed, or <see langword="null"/> when successful.</param>
    /// <returns><see langword="true"/> if the result is a failure; otherwise, <see langword="false"/>.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetErrors([NotNullWhen(true)] out IReadOnlyList<Error>? errors) {
        if(!this._isSuccess) {
            errors = this.Errors;
            return true;
        }

        errors = null;
        return false;
    }

    // ── Implicit operators ────────────────────────────────────────────────────

    /// <summary>
    /// Implicitly converts a success value to a successful <see cref="Result{TValue}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(TValue value) {
        return new(value);
    }

    /// <summary>
    /// Implicitly converts a single <see cref="Error"/> to a failed <see cref="Result{TValue}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(Error error) {
        return new(error);
    }

    /// <summary>
    /// Implicitly converts a list of <see cref="Error"/>s to a failed <see cref="Result{TValue}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(List<Error> errors) {
        return new(errors);
    }

    /// <summary>
    /// Implicitly converts an array of <see cref="Error"/>s to a failed <see cref="Result{TValue}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(Error[] errors) {
        return new([.. errors]);
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a successful <see cref="Result{TValue}"/> containing the specified value.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue> Success(TValue value) {
        return new(value);
    }

    // ── Core Combinators ──────────────────────────────────────────────────────

    /// <summary>
    /// Executes <paramref name="onValue"/> when successful, or <paramref name="onError"/> when failed.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Match<TResult>(
        Func<TValue, TResult> onValue,
        Func<IReadOnlyList<Error>, TResult> onError) {
        return !this._isSuccess ? onError(this.Errors) : onValue(this._value!);
    }

    /// <summary>
    /// Executes an action based on the result state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Switch(
        Action<TValue> onValue,
        Action<IReadOnlyList<Error>> onError) {
        if(!this._isSuccess) onError(this.Errors);
        else onValue(this._value!);
    }

    /// <summary>
    /// Chains the next operation if this result is successful.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TNextValue> Then<TNextValue>(Func<TValue, Result<TNextValue>> next) {
        if(!this._isSuccess) return ToFailure<TNextValue>();
        return next(this._value!);
    }

    /// <summary>
    /// Transforms the success value using the specified mapper.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TNew> Map<TNew>(Func<TValue, TNew> mapper) {
        if(!this._isSuccess) return ToFailure<TNew>();
        return mapper(this._value!);
    }

    /// <summary>
    /// Executes <paramref name="action"/> as a side-effect when successful and returns self.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> Do(Action<TValue> action) {
        if(this._isSuccess) action(this._value!);
        return this;
    }

    /// <summary>
    /// Alias for <see cref="Do(Action{TValue})"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> Tap(Action<TValue> action) {
        return Do(action);
    }

    /// <summary>
    /// Executes a parameterless side-effect when successful and returns self.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> Do(Action action) {
        if(this._isSuccess) action();
        return this;
    }

    /// <summary>
    /// Alias for <see cref="Do(Action)"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> Tap(Action action) {
        return Do(action);
    }

    /// <summary>
    /// Validates a condition against the value. Returns <paramref name="error"/> when false.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> Ensure(Func<TValue, bool> predicate, Error error) {
        if(!this._isSuccess) return this;
        if(!predicate(this._value!)) return error;
        return this;
    }

    /// <summary>
    /// Attempts to recover from a failure by returning a fallback value.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> Recover(Func<IReadOnlyList<Error>, TValue> recover) {
        if(this._isSuccess) return this;
        return recover(this.Errors);
    }

    /// <summary>
    /// Executes <paramref name="action"/> only when successful. Alias for <see cref="Do(Action{TValue})"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> IfSuccess(Action<TValue> action) {
        return Do(action);
    }

    /// <summary>
    /// Executes <paramref name="action"/> only when failed and returns self.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> IfFailure(Action<IReadOnlyList<Error>> action) {
        if(!this._isSuccess) action(this.Errors);
        return this;
    }

    /// <summary>
    /// Alias for <see cref="IfFailure(Action{IReadOnlyList{Error}})"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> TapError(Action<IReadOnlyList<Error>> action) {
        return IfFailure(action);
    }

    // ── Equality & HashCode ───────────────────────────────────────────────────

    /// <inheritdoc/>
    [Pure]
    public bool Equals(Result<TValue> other) {
        if(this._isSuccess != other._isSuccess) return false;
        return this._isSuccess
            ? EqualityComparer<TValue>.Default.Equals(this._value!, other._value!)
            : this.Errors.SequenceEqual(other.Errors);
    }

    /// <inheritdoc/>
    [Pure]
    public override int GetHashCode() {
        if(this._isSuccess)
            return HashCode.Combine(true, this._value);

        HashCode hash = new();
        hash.Add(false);

        foreach(Error error in this.Errors)
            hash.Add(error);

        return hash.ToHashCode();
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes <paramref name="action"/> on the value and disposes it if it implements <see cref="IDisposable"/>.
    /// </summary>
    public Result<TValue> Consume(Action<TValue> action) {
        if(this._isSuccess) {
            using(this._value as IDisposable) {
                action(this._value!);
            }
        }
        return this;
    }

    /// <summary>
    /// Executes <paramref name="action"/> on the value and asynchronously disposes it.
    /// </summary>
    public async ValueTask ConsumeAsync(
        Func<TValue, CancellationToken, ValueTask> action,
        CancellationToken cancellationToken = default) {

        if(!this._isSuccess) return;

        if(this._value is IAsyncDisposable asyncDisposable) {
            await using(asyncDisposable.ConfigureAwait(false)) {
                await action(this._value!, cancellationToken).ConfigureAwait(false);
            }
        }
        else {
            using(this._value as IDisposable) {
                await action(this._value!, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Disposes the value if it implements <see cref="IDisposable"/>.
    /// </summary>
    public void DisposeValue() {
        if(this._isSuccess && this._value is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>
    /// Asynchronously disposes the value.
    /// </summary>
    public ValueTask DisposeValueAsync() {
        if(!this._isSuccess) return ValueTask.CompletedTask;

        if(this._value is IAsyncDisposable asyncDisposable)
            return asyncDisposable.DisposeAsync();

        if(this._value is IDisposable disposable)
            disposable.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    [Pure]
    public override string ToString() {
        return this._isSuccess
            ? $"Success({this._value})"
            : $"Failure({string.Join(", ", this.Errors.Select(e => e.Code))})";
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Result<TNew> ToFailure<TNew>() {
        if(this._multipleErrors is not null)
            return new(this._multipleErrors);

        if(!this._singleError.Equals(default))
            return new(this._singleError);

        return new(Error.Uninitialized);
    }

    // ── Exception Throw Helpers (JIT Cold Path Optimization) ──────────────────

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowValueOnFailureException() {
        throw new InvalidOperationException(
            "Cannot access the value of an error result. Check IsSuccess before accessing Value.");
    }

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowErrorOnSuccessException() {
        throw new InvalidOperationException(
            "Cannot access an error of a successful result. Check IsFailure before accessing FirstError.");
    }

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUninitializedException() {
        throw new InvalidOperationException(
            "Result is in an uninitialized state. Do not use default struct constructors.");
    }

    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowEmptyErrorListException() {
        throw new ArgumentException(
            "At least one error is required to create a failed result.", "errors");
    }
}