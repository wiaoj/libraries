namespace Wiaoj.Results;
public static partial class ResultsExtensions {

    // --- Bridge Methods (Köprüler) ---

    /// <summary>
    /// Wraps a value in a successful Result.
    /// </summary>
    public static Result<T> AsResult<T>(this T value) {
        return Result.Success(value);
    }

    /// <summary>
    /// Wraps a Task of T in a Task of Result.
    /// </summary>
    public static async Task<Result<T>> AsResult<T>(this Task<T> valueTask) {
        T? value = await valueTask.ConfigureAwait(false);
        return Result.Success(value);
    }

    /// <summary>
    /// Wraps a Result in a Task.
    /// </summary>
    public static Task<Result<T>> AsTask<T>(this Result<T> result) {
        return Task.FromResult(result);
    }

    // --- Null Safety ---

    /// <summary>
    /// Converts a Result of nullable T to a Result of T by ensuring the value is not null.
    /// </summary>
    public static Result<T> EnsureNotNull<T>(this Result<T?> result, Error error) where T : class {
        if(result.IsError)
            return result.Errors.ToList();

        if(result.Value is null)
            return error;

        return Result.Success(result.Value);
    }

    public static async Task<Result<T>> EnsureNotNullAsync<T>(this Task<Result<T?>> resultTask, Error error) where T : class {
        Result<T?> result = await resultTask.ConfigureAwait(false);
        return result.EnsureNotNull(error);
    }

    // --- Error Mapping ---

    public static Result<T> MapError<T>(this Result<T> result, Error error) {
        if(result.IsError)
            return error;
        return result;
    }

    public static Result<T> MapError<T>(this Result<T> result, Func<Error, Error> errorMapper) {
        if(result.IsError)
            return errorMapper(result.FirstError);
        return result;
    }

    // --- Chaining (Then / Bind) ---

    // 1. Task<Result> -> Func -> Result (Zincirleme: Asenkron sonucu bekle, sonra senkron Result dönen metodu çalıştır)
    public static async Task<Result<TNextValue>> ThenAsync<TValue, TNextValue>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, Result<TNextValue>> next) {

        Result<TValue> result = await resultTask.ConfigureAwait(false);

        if(result.IsError) {
            return result.Errors.ToList();
        }

        return next(result.Value);
    }

    // 2. Result -> Func -> Task<Result> (Klasik Async Bind)
    public static async Task<Result<TNextValue>> ThenAsync<TValue, TNextValue>(
        this Result<TValue> result,
        Func<TValue, Task<Result<TNextValue>>> next) {

        if(result.IsError) {
            return result.Errors.ToList();
        }
        return await next(result.Value).ConfigureAwait(false);
    }

    // 3. Result -> Func(..., ct) -> Task<Result> (CancellationToken Destekli)
    public static async Task<Result<TNextValue>> ThenAsync<TValue, TNextValue>(
        this Result<TValue> result,
        Func<TValue, CancellationToken, Task<Result<TNextValue>>> next,
        CancellationToken ct) {

        if(result.IsError) {
            return result.Errors.ToList();
        }

        return await next(result.Value, ct).ConfigureAwait(false);
    }

    // 4. Result -> Func -> Task<T> (Değer dönüşümü yapan asenkron metod)
    public static async Task<Result<TNextValue>> ThenAsync<TValue, TNextValue>(
        this Result<TValue> result,
        Func<TValue, Task<TNextValue>> next) {

        if(result.IsError) {
            return result.Errors.ToList();
        }

        TNextValue value = await next(result.Value).ConfigureAwait(false);
        return Result.Success(value);
    }

    // 5. Result -> Func(..., ct) -> Task<T> (CancellationToken Destekli Değer Dönüşümü)
    public static async Task<Result<TNextValue>> ThenAsync<TValue, TNextValue>(
        this Result<TValue> result,
        Func<TValue, CancellationToken, Task<TNextValue>> next,
        CancellationToken ct) {

        if(result.IsError) {
            return result.Errors.ToList();
        }

        TNextValue value = await next(result.Value, ct).ConfigureAwait(false);
        return Result.Success(value);
    }

    // --- Mapping ---

    // 1. Task<Result> -> Func -> Result (Asenkron sonucu bekle, değeri dönüştür)
    public static async Task<Result<TNewValue>> MapAsync<TValue, TNewValue>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, TNewValue> mapper) {

        Result<TValue> result = await resultTask.ConfigureAwait(false);

        if(result.IsError) {
            return result.Errors.ToList();
        }

        return Result.Success(mapper(result.Value));
    }

    // 2. Task<Result> -> Func -> Result<New> (Flattening: Mapper Result dönüyorsa iç içe Result olmasın diye)
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, Result<TNew>> mapper) {

        Result<T> result = await resultTask.ConfigureAwait(false);
        if(result.IsError) return result.Errors.ToList();

        return mapper(result.Value);
    }

    // --- Side Effects (Do) ---

    // Asenkron işlemin bitmesini bekle, başarılıysa SENKRON bir işlem yap (Loglama vb.)
    public static async Task<Result<TValue>> DoAsync<TValue>(
        this Task<Result<TValue>> resultTask,
        Action<TValue> action) {

        Result<TValue> result = await resultTask.ConfigureAwait(false);

        if(result.IsSuccess) {
            action(result.Value);
        }

        return result;
    }

    // --- Validation (Ensure) ---

    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error) {
        if(result.IsError) return result;

        if(!predicate(result.Value)) {
            return error;
        }

        return result;
    }

    public static Result<T> Ensure<T>(this Result<T> result, Func<bool> predicate, Error error) {
        if(result.IsError) return result;

        if(!predicate()) {
            return error;
        }

        return result;
    }

    // 1. Task<Result> -> Predicate (Senkron)
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, bool> predicate,
        Error error) {

        Result<T> result = await resultTask.ConfigureAwait(false);

        if(result.IsError) return result;

        if(!predicate(result.Value)) {
            return error;
        }

        return result;
    }

    // 2. Result -> Predicate (Asenkron)
    public static async Task<Result<T>> EnsureAsync<T>(
        this Result<T> result,
        Func<T, Task<bool>> predicate,
        Error error) {

        if(result.IsError) return result;

        if(!await predicate(result.Value).ConfigureAwait(false)) {
            return error;
        }

        return result;
    }

    // 3. Task<Result> -> Predicate (Asenkron)
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> resultTask,
        Func<T, Task<bool>> predicate,
        Error error) {

        Result<T> result = await resultTask.ConfigureAwait(false);
        if(result.IsError) return result;

        if(!await predicate(result.Value).ConfigureAwait(false)) {
            return error;
        }

        return result;
    }

    // 4. Task<Result> -> Predicate (Async) -> Dynamic Error Factory (Async)
    public static async Task<Result<TValue>> EnsureWithAsync<TValue>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, Task<bool>> predicate,
        Func<TValue, Task<Error>> errorFactory) {

        Result<TValue> result = await resultTask.ConfigureAwait(false);
        if(result.IsError) return result;

        if(!await predicate(result.Value).ConfigureAwait(false)) {
            return await errorFactory(result.Value).ConfigureAwait(false);
        }

        return result;
    }
}