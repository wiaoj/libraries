using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Wiaoj.Results;

/// <summary>
/// Provides extension methods for handling asynchronous operations with <see cref="Result{TValue}"/>.
/// </summary>
public static partial class ResultsExtensions {

    // CancellationToken desteği için Task'ı beklemeden önce kontrol yapmak için (Opsiyonel: Ancak ThenAsync'in temel rolü bu değil)
    // Bu metodun temel rolü Func'ı çağırmak olduğu için, genellikle token'ı Func'a aktarmak en iyisidir.
    // Ancak geriye dönük uyumluluk adına ThenAsync'i değiştirmeden MatchAsync'i token alacak şekilde güncelledik.

    /// <summary>
    /// Asynchronously awaits a result and, if successful, executes the next asynchronous operation.
    /// If the initial result is an error, the errors are propagated and the next step is skipped.
    /// </summary>
    /// <typeparam name="T">The type of the current value.</typeparam>
    /// <typeparam name="TNext">The type of the next value.</typeparam>
    /// <param name="task">The task representing the current result.</param>
    /// <param name="next">The asynchronous function to execute if the current result is success.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the result of the chain.</returns>
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
    this Task<Result<T>> task,
    Func<T, Task<Result<TNext>>> next,
        CancellationToken cancellationToken = default) { 

        Result<T> result = await task.ConfigureAwait(false);
        if (result.IsError) {
            return result.Errors.ToList();
        }

        // Bu noktada iptal kontrolü, zincirin devam etmesini engeller.
        cancellationToken.ThrowIfCancellationRequested();

        return await next(result.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Chains an asynchronous operation to a synchronous result.
    /// </summary>
    /// <typeparam name="T">The type of the current value.</typeparam>
    /// <typeparam name="TNext">The type of the next value.</typeparam>
    /// <param name="result">The current synchronous result.</param>
    /// <param name="next">The asynchronous function to execute if the current result is success.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the result of the chain.</returns>
    public static async Task<Result<TNext>> ThenAsync<T, TNext>(
    this Result<T> result,
    Func<T, Task<Result<TNext>>> next,
        CancellationToken cancellationToken = default) { 

        if (result.IsError) {
            return result.Errors.ToList();
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await next(result.Value).ConfigureAwait(false);
    }

    // --- MatchAsync Güncellemeleri ---

    /// <summary>
    /// Asynchronously matches the result to a value or an error handler.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TResult">The return type of the match.</typeparam>
    /// <param name="task">The task representing the result.</param>
    /// <param name="onValue">The function to execute if successful.</param>
    /// <param name="onError">The function to execute if failed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The result of the match operation.</returns>
    public static async Task<TResult> MatchAsync<T, TResult>(
    this Task<Result<T>> task,
    Func<T, TResult> onValue,
    Func<IReadOnlyList<Error>, TResult> onError,
        CancellationToken cancellationToken = default) { 

        Result<T> result = await task.ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested(); // Match'ten hemen önce kontrol

        return result.Match(onValue, onError);
    }

    /// <summary>
    /// Asynchronously matches the result to an asynchronous value or error handler.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <typeparam name="TResult">The return type of the match.</typeparam>
    /// <param name="task">The task representing the result.</param>
    /// <param name="onValue">The asynchronous function to execute if successful.</param>
    /// <param name="onError">The asynchronous function to execute if failed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The result of the match operation.</returns>
    public static async Task<TResult> MatchAsync<T, TResult>(
    this Task<Result<T>> task,
    Func<T, Task<TResult>> onValue,
    Func<IReadOnlyList<Error>, Task<TResult>> onError,
        CancellationToken cancellationToken = default) { 

        Result<T> result = await task.ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested(); // Match'ten hemen önce kontrol

        if (result.IsError) {
            return await onError(result.Errors).ConfigureAwait(false);
        }

        return await onValue(result.Value).ConfigureAwait(false);
    }

    // --- MapSuccess Güncellemeleri ---

    /// <summary>
    /// Discards the value of the result and converts it to a <see cref="Result{Success}"/>.
    /// Useful when the value is no longer needed, and you want to return a void-like result.
    /// </summary>
    /// <typeparam name="T">The type of the current value.</typeparam>
    /// <param name="result">The current result.</param>
    /// <returns>A <see cref="Result{Success}"/> representing success or the existing errors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<Success> MapSuccess<T>(this Result<T> result) {
        return result.Map(_ => Success.Default);
    }

    /// <summary>
    /// Asynchronously discards the value of the result and converts it to a <see cref="Result{Success}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the current value.</typeparam>
    /// <param name="task">The task representing the current result.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task containing a <see cref="Result{Success}"/>.</returns>
    public static async Task<Result<Success>> MapSuccessAsync<T>(
        this Task<Result<T>> task,
        CancellationToken cancellationToken = default) { 

        var result = await task.ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        return result.Map(_ => Success.Default);
    }

    // --- DoAsync Güncellemeleri ---
    // (Func<T, Task> ve Func<Task> imzaları, CancellationToken alacak şekilde Aşırı Yüklendi)

    // Aşırı Yükleme 1: T değeri ve CancellationToken alan aksiyon
    public static async Task<Result<T>> DoAsync<T>(
        this Task<Result<T>> task,
        Func<T, CancellationToken, Task> action, // Değişti: Token parametresi eklendi
        CancellationToken cancellationToken = default) { 

        var result = await task.ConfigureAwait(false);

        if (result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Value, cancellationToken).ConfigureAwait(false); // Token iletildi
        }

        return result;
    }

    // Aşırı Yükleme 2: T değeri ve CancellationToken alan senkron sonuç aksiyonu
    public static async Task<Result<T>> DoAsync<T>(
        this Result<T> result,
        Func<T, CancellationToken, Task> action, // Değişti: Token parametresi eklendi
        CancellationToken cancellationToken = default) { 

        if (result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(result.Value, cancellationToken).ConfigureAwait(false); // Token iletildi
        }

        return result;
    }

    // Aşırı Yükleme 3: Parametresiz (sadece CancellationToken alan) aksiyon
    public static async Task<Result<T>> DoAsync<T>(
        this Task<Result<T>> task,
        Func<CancellationToken, Task> action, // Değişti: Token parametresi eklendi
        CancellationToken cancellationToken = default) { 

        var result = await task.ConfigureAwait(false);

        if (result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(cancellationToken).ConfigureAwait(false); // Token iletildi
        }

        return result;
    }

    // Aşırı Yükleme 4: Parametresiz (sadece CancellationToken alan) senkron sonuç aksiyonu
    public static async Task<Result<T>> DoAsync<T>(
        this Result<T> result,
        Func<CancellationToken, Task> action, // Değişti: Token parametresi eklendi
        CancellationToken cancellationToken = default) { 

        if (result.IsSuccess) {
            cancellationToken.ThrowIfCancellationRequested();
            await action(cancellationToken).ConfigureAwait(false); // Token iletildi
        }

        return result;
    }

    // --- IfSuccess/IfFailureAsync Güncellemeleri ---

    public static async Task<Result<T>> IfSuccessAsync<T>(
   this Task<Result<T>> task,
   Action<T> action,
      CancellationToken cancellationToken = default) { 

        var result = await task.ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (result.IsSuccess) {
            action(result.Value);
        }
        return result;
    }

    public static async Task<Result<T>> IfFailureAsync<T>(
      this Task<Result<T>> task,
      Action<IReadOnlyList<Error>> action,
          CancellationToken cancellationToken = default) { 

        var result = await task.ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (result.IsError) {
            action(result.Errors);
        }
        return result;
    }
}