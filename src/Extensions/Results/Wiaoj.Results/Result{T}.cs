using System.Diagnostics.CodeAnalysis;
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
    private readonly List<Error>? _errors;

    /// <summary>
    /// Gets a value indicating whether the result represents a failure.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_errors))]
    [MemberNotNullWhen(false, nameof(_value))]
    public bool IsError => _errors is not null;

    /// <summary>
    /// Gets a value indicating whether the result represents a success.
    /// </summary>
    [MemberNotNullWhen(false, nameof(_errors))]
    public bool IsSuccess => _errors is null;

    /// <summary>
    /// Gets the success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="IsError"/> is <c>true</c>.</exception>
    public TValue Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsSuccess
            ? _value!
            : throw new InvalidOperationException(
                "Cannot access the value of an error result. Check IsSuccess before accessing Value.");
    }

    /// <summary>
    /// Gets the first error.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="IsSuccess"/> is <c>true</c>.</exception>
    public Error FirstError {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsError
            ? _errors[0]
            : throw new InvalidOperationException(
                "Cannot access an error of a successful result. Check IsError before accessing FirstError.");
    }

    /// <summary>
    /// Gets the list of errors. Returns an empty list when <see cref="IsSuccess"/> is <c>true</c>.
    /// </summary>
    public IReadOnlyList<Error> Errors {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _errors ?? (IReadOnlyList<Error>)[];
    }

    private Result(TValue value) {
        _value = value;
        _errors = null;
    }

    private Result(List<Error> errors) {
        if(errors is null || errors.Count == 0)
            throw new ArgumentException(
                "At least one error is required to create a failed result.", nameof(errors));

        _value = default;
        _errors = errors;
    }

    // ── Implicit operators ────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(TValue value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(Error error) => new([error]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(List<Error> errors) => new(errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TValue>(Error[] errors) => new([.. errors]);

    // ── Factory ───────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TValue> Success(TValue value) => new(value);

    // ── ROP core ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies <paramref name="onValue"/> if successful, or <paramref name="onError"/> if failed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Match<TResult>(
        Func<TValue, TResult> onValue,
        Func<IReadOnlyList<Error>, TResult> onError) {
        return IsError ? onError(_errors) : onValue(_value!);
    }

    /// <summary>
    /// Executes an action based on the result state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Switch(
        Action<TValue> onValue,
        Action<IReadOnlyList<Error>> onError) {
        if(IsError) onError(_errors);
        else onValue(_value!);
    }

    // ── Combinators ───────────────────────────────────────────────────────────

    /// <summary>
    /// Chains to the next operation. Propagates errors without calling <paramref name="next"/>.
    /// Equivalent to <c>Bind</c> or <c>FlatMap</c>.
    /// </summary>
    public Result<TNextValue> Then<TNextValue>(Func<TValue, Result<TNextValue>> next) {
        if(IsError) return _errors;
        return next(_value!);
    }

    /// <summary>
    /// Transforms the success value. Does not allow returning an error —
    /// use <see cref="Then{TNextValue}"/> when the transformation may fail.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<TValue, TNew> mapper) {
        if(IsError) return _errors;
        return mapper(_value!);
    }

    /// <summary>
    /// Executes <paramref name="action"/> as a side-effect when successful.
    /// Does not change the result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> Do(Action<TValue> action) {
        if(IsSuccess) action(_value!);
        return this;
    }

    /// <summary>
    /// Executes a parameterless side-effect when successful.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> Do(Action action) {
        if(IsSuccess) action();
        return this;
    }

    /// <summary>
    /// Validates a condition against the value. Returns <paramref name="error"/> when
    /// <paramref name="predicate"/> is <c>false</c>.
    /// </summary>
    public Result<TValue> Ensure(Func<TValue, bool> predicate, Error error) {
        if(IsError) return this;
        if(!predicate(_value!)) return error;
        return this;
    }

    /// <summary>
    /// Attempts to recover from a failure by returning a fallback value.
    /// </summary>
    public Result<TValue> Recover(Func<IReadOnlyList<Error>, TValue> recover) {
        if(IsSuccess) return this;
        return recover(_errors);
    }

    /// <summary>
    /// Executes <paramref name="action"/> only when successful. Alias for <see cref="Do(Action{TValue})"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> IfSuccess(Action<TValue> action) => Do(action);

    /// <summary>
    /// Executes <paramref name="action"/> only when failed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<TValue> IfFailure(Action<IReadOnlyList<Error>> action) {
        if(IsError) action(_errors);
        return this;
    }
    // ── Equality ──────────────────────────────────────────────────────────────
    // record struct auto-generates equality by comparing fields.
    // _errors is a List<Error> — reference equality would make two Results with
    // the same errors unequal. Override to compare list contents.

    public bool Equals(Result<TValue> other) {
        if(IsSuccess != other.IsSuccess) return false;

        if(IsSuccess)
            return EqualityComparer<TValue>.Default.Equals(_value!, other._value!);

        return _errors!.SequenceEqual(other._errors!);
    }

    public override int GetHashCode() {
        if(IsSuccess)
            return HashCode.Combine(true, _value);

        HashCode hash = new();
        hash.Add(false);
        foreach(Error e in _errors!)
            hash.Add(e);
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
        if(IsSuccess) {
            using(_value as IDisposable) {
                action(_value!);
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

        if(!IsSuccess) return;

        if(_value is IAsyncDisposable asyncDisposable) {
            await using(asyncDisposable.ConfigureAwait(false)) {
                await action(_value!, cancellationToken).ConfigureAwait(false);
            }
        }
        else {
            using(_value as IDisposable) {
                await action(_value!, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Disposes the value if it implements <see cref="IDisposable"/>.
    /// Use this when you have already used the value and need to release it explicitly.
    /// </summary>
    public void DisposeValue() {
        if(IsSuccess && _value is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>
    /// Asynchronously disposes the value.
    /// Prefers <see cref="IAsyncDisposable"/>, falls back to <see cref="IDisposable"/>.
    /// </summary>
    public ValueTask DisposeValueAsync() {
        if(!IsSuccess) return ValueTask.CompletedTask;

        if(_value is IAsyncDisposable asyncDisposable)
            return asyncDisposable.DisposeAsync();

        if(_value is IDisposable disposable)
            disposable.Dispose();

        return ValueTask.CompletedTask;
    }

}