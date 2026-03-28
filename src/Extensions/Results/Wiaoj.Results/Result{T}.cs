using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results;
/// <summary>
/// Represents the result of an operation: either a successful value (<typeparamref name="TValue"/>)
/// or a non-empty list of <see cref="Error"/>s.
/// <para>
/// This struct is the core primitive for Railway Oriented Programming.
/// Use <see cref="Match{TResult}"/>, <see cref="Then{TNextValue}"/>, and <see cref="Map{TNew}"/>
/// to chain operations without nested <c>if</c> checks.
/// </para>
/// </summary>
/// <typeparam name="TValue">The type of the underlying success value.</typeparam> 
public readonly record struct Result<TValue> : IResult {
    private readonly TValue? _value;
    private readonly Error _singleError;
    private readonly List<Error>? _multipleErrors;
    private readonly bool _isFailure;  

    /// <summary>
    /// Gets a value indicating whether the result represents a failure.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_multipleErrors))]
    [MemberNotNullWhen(false, nameof(_value))]
    public bool IsFailure => this._isFailure;

    /// <summary>
    /// Gets a value indicating whether the result represents a success.
    /// </summary>
    [MemberNotNullWhen(false, nameof(_multipleErrors))]
    public bool IsSuccess => !this._isFailure;

    /// <summary>
    /// Gets the success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="IsFailure"/> is <c>true</c>.</exception>
    public TValue Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.IsSuccess
            ? this._value!
            : throw new InvalidOperationException(
                "Cannot access the value of an error result. Check IsSuccess before accessing Value.");
    }

    /// <summary>
    /// Gets the first error.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="IsSuccess"/> is <c>true</c>.</exception>
    public Error FirstError {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.IsFailure
            ? (this._multipleErrors is not null ? this._multipleErrors[0] : this._singleError)
            : throw new InvalidOperationException(
                "Cannot access an error of a successful result. Check IsFailure before accessing FirstError.");
    }

    /// <summary>
    /// Gets the list of errors. Returns an empty list when <see cref="IsSuccess"/> is <c>true</c>.
    /// </summary>
    public IReadOnlyList<Error> Errors {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            if(this.IsSuccess) return [];
            if(this._multipleErrors is not null) return this._multipleErrors;
            return [this._singleError];
        }
    }

    private Result(TValue value) {
        this._isFailure = false;
        this._value = value;
        this._singleError = default;
        this._multipleErrors = null;
    }

    private Result(Error error) {
        this._isFailure = true;
        this._value = default;
        this._singleError = error;
        this._multipleErrors = null;
    }

    private Result(params List<Error> errors) {
        if(errors is null || errors.Count == 0)
            throw new ArgumentException("At least one error is required to create a failed result.", nameof(errors));

        this._isFailure = true;
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

    // ── Implicit operators ────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(TValue value) {
        return new(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(Error error) {
        return new(error);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(List<Error> errors) {
        return new(errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(Error[] errors) {
        return new([.. errors]);
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a successful <see cref="Result{TValue}"/> with the specified value.
    /// </summary>
    /// <param name="value">The success value to wrap.</param>
    /// <returns>A successful result containing the specified value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue> Success(TValue value) {
        return new(value);
    }

    // ── ROP core ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies <paramref name="onValue"/> if successful, or <paramref name="onError"/> if failed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Match<TResult>(
        Func<TValue, TResult> onValue,
        Func<IReadOnlyList<Error>, TResult> onError) {
        return this.IsFailure ? onError(Errors) : onValue(this._value);
    }

    /// <summary>
    /// Executes an action based on the result state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Switch(
        Action<TValue> onValue,
        Action<IReadOnlyList<Error>> onError) {
        if(this.IsFailure) onError(Errors);
        else onValue(this._value);
    }

    // ── Combinators ───────────────────────────────────────────────────────────

    /// <summary>
    /// Chains to the next operation. Propagates errors without calling <paramref name="next"/>.
    /// Equivalent to <c>Bind</c> or <c>FlatMap</c>.
    /// </summary>
    [Pure]
    public Result<TNextValue> Then<TNextValue>(Func<TValue, Result<TNextValue>> next) {
        if(this.IsFailure) return Fail<TNextValue>();
        return next(this._value);
    }

    /// <summary>
    /// Transforms the success value. Does not allow returning an error —
    /// use <see cref="Then{TNextValue}"/> when the transformation may fail.
    /// </summary>
    [Pure]
    public Result<TNew> Map<TNew>(Func<TValue, TNew> mapper) {
        if(this.IsFailure) return Fail<TNew>();
        return mapper(this._value);
    }

    /// <summary>
    /// Executes <paramref name="action"/> as a side-effect when successful.
    /// Does not change the result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> Do(Action<TValue> action) {
        if(this.IsSuccess) action(this._value);
        return this;
    }

    /// <summary>
    /// Executes a parameterless side-effect when successful.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> Do(Action action) {
        if(this.IsSuccess) action();
        return this;
    }

    /// <summary>
    /// Validates a condition against the value. Returns <paramref name="error"/> when
    /// <paramref name="predicate"/> is <c>false</c>.
    /// </summary>
    [Pure]
    public Result<TValue> Ensure(Func<TValue, bool> predicate, Error error) {
        if(this.IsFailure) return this;
        if(!predicate(this._value)) return error;
        return this;
    }

    /// <summary>
    /// Attempts to recover from a failure by returning a fallback value.
    /// </summary>
    [Pure]
    public Result<TValue> Recover(Func<IReadOnlyList<Error>, TValue> recover) {
        if(this.IsSuccess) return this;
        return recover(this._multipleErrors);
    }

    /// <summary>
    /// Executes <paramref name="action"/> only when successful. Alias for <see cref="Do(Action{TValue})"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> IfSuccess(Action<TValue> action) {
        return Do(action);
    }

    /// <summary>
    /// Executes <paramref name="action"/> only when failed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> IfFailure(Action<IReadOnlyList<Error>> action) {
        if(this.IsFailure) action(this._multipleErrors);
        return this;
    }
    /// <summary>
    /// Determines whether the specified <see cref="Result{TValue}"/> is equal to the current result.
    /// </summary>
    /// <remarks>
    /// Two results are equal if they represent the same state (success or failure) and contain equal data.
    /// For successful results, values are compared using <see cref="EqualityComparer{T}.Default"/>.
    /// For failed results, errors are compared sequentially using <see cref="Enumerable.SequenceEqual{TSource}(IEnumerable{TSource}, IEnumerable{TSource})"/>.
    /// </remarks>
    /// <param name="other">The <see cref="Result{TValue}"/> to compare with the current instance.</param>
    /// <returns><c>true</c> if both results are equal; otherwise, <c>false</c>.</returns>
    public bool Equals(Result<TValue> other) {
        if(this.IsSuccess != other.IsSuccess) return false;
        return this.IsSuccess
            ? EqualityComparer<TValue>.Default.Equals(this._value!, other._value!)
            : this.Errors.SequenceEqual(other.Errors);
    }


    /// <summary>
    /// Serves as the default hash function for this result.
    /// </summary>
    /// <remarks>
    /// The hash code is computed based on the result's state and data:
    /// <list type="bullet">
    /// <item><description>For successful results: combines the success state with the value's hash code.</description></item>
    /// <item><description>For failed results: combines the failure state with the hash codes of all errors in sequence.</description></item>
    /// </list>
    /// </remarks>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() {
        if(this.IsSuccess)
            return HashCode.Combine(true, this._value);

        HashCode hash = new();
        hash.Add(false);

        foreach(var error in this.Errors)
            hash.Add(error);

        return hash.ToHashCode();
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes <paramref name="action"/> on the value and disposes it afterwards
    /// if it implements <see cref="IDisposable"/>.
    /// <para>
    /// Use this when the value is a short-lived resource (e.g., <see cref="System.IO.Stream"/>,
    /// <see cref="System.Net.Http.HttpResponseMessage"/>) that must be released after a single use.
    /// </para>
    /// </summary>
    public Result<TValue> Consume(Action<TValue> action) {
        if(this.IsSuccess) {
            using(this._value as IDisposable) {
                action(this._value!);
            }
        }
        return this;
    }

    /// <summary>
    /// Executes <paramref name="action"/> on the value and disposes it afterwards,
    /// preferring <see cref="IAsyncDisposable"/> over <see cref="IDisposable"/>.
    /// </summary>
    public async ValueTask ConsumeAsync(
        Func<TValue, CancellationToken, ValueTask> action,
        CancellationToken cancellationToken = default) {

        if(!this.IsSuccess) return;

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
    /// Use this when you have already used the value and need to release it explicitly.
    /// </summary>
    public void DisposeValue() {
        if(this.IsSuccess && this._value is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>
    /// Asynchronously disposes the value.
    /// Prefers <see cref="IAsyncDisposable"/>, falls back to <see cref="IDisposable"/>.
    /// </summary>
    public ValueTask DisposeValueAsync() {
        if(!this.IsSuccess) return ValueTask.CompletedTask;

        if(this._value is IAsyncDisposable asyncDisposable)
            return asyncDisposable.DisposeAsync();

        if(this._value is IDisposable disposable)
            disposable.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override string ToString() {
        return this.IsSuccess ? "Success" : $"Failure ({this.Errors.Count} errors)";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Result<TNew> Fail<TNew>() {
        return this._multipleErrors is not null
            ? new(this._multipleErrors)
            : new(this._singleError);
    }
}