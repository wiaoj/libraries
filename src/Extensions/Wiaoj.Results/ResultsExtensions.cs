//namespace Wiaoj.Results;

///// <summary>
///// Provides fluent extension methods for asynchronous operations and interoperability between result types.
///// </summary>
//public static class ResultsExtensions {
//    #region ErrorOr Async Extensions

//    /// <summary>
//    /// Awaits a Task&lt;ErrorOr&lt;TValue&gt;&gt; and chains the next asynchronous operation if the result is successful.
//    /// </summary>
//    /// <remarks>
//    /// Use Case: After fetching a user from a database, get their permissions from another async service.
//    /// <code>
//    /// ErrorOr&lt;Permissions&gt; result = await GetUserByIdAsync(id)
//    ///     .ThenAsync(user => GetPermissionsAsync(user.Id));
//    /// </code>
//    /// </remarks>
//    public static async Task<ErrorOr<TNextValue>> ThenAsync<TValue, TNextValue>(
//        this Task<ErrorOr<TValue>> errorOrTask,
//        Func<TValue, Task<ErrorOr<TNextValue>>> next) {
//        ErrorOr<TValue> result = await errorOrTask.ConfigureAwait(false);
//        return result.IsError ? result.Errors : await next(result.Value).ConfigureAwait(false);
//    }

//    /// <summary>
//    /// Awaits a Task&lt;ErrorOr&lt;TValue&gt;&gt; and transforms the successful value using an async selector. (LINQ.SelectAsync)
//    /// </summary>
//    public static async Task<ErrorOr<TResult>> SelectAsync<TValue, TResult>(
//        this Task<ErrorOr<TValue>> errorOrTask,
//        Func<TValue, Task<TResult>> selector) {
//        ErrorOr<TValue> result = await errorOrTask.ConfigureAwait(false);
//        return result.IsError ? new ErrorOr<TResult>(result.Errors) : await selector(result.Value).ConfigureAwait(false);
//    }

//    /// <summary>
//    /// Awaits a Task&lt;ErrorOr&lt;TValue&gt;&gt; and matches the result to one of two async functions.
//    /// </summary>
//    public static async Task<TResult> MatchAsync<TValue, TResult>(
//        this Task<ErrorOr<TValue>> errorOrTask,
//        Func<TValue, Task<TResult>> onValue,
//        Func<IReadOnlyList<Error>, Task<TResult>> onError) {
//        ErrorOr<TValue> result = await errorOrTask.ConfigureAwait(false);
//        return await (result.IsError ? onError(result.Errors) : onValue(result.Value)).ConfigureAwait(false);
//    }

//    /// <summary>
//    /// Awaits a Task&lt;ErrorOr&lt;TValue&gt;&gt; and executes an asynchronous side-effect action on the successful value.
//    /// </summary>
//    public static async Task<ErrorOr<TValue>> TapAsync<TValue>(
//        this Task<ErrorOr<TValue>> errorOrTask,
//        Func<TValue, Task> action) {
//        ErrorOr<TValue> result = await errorOrTask.ConfigureAwait(false);
//        if (!result.IsError) {
//            await action(result.Value).ConfigureAwait(false);
//        }
//        return result;
//    }

//    // NOTE: For performance-critical paths, consider adding ValueTask<T> overloads as well.
//    // e.g., public static async ValueTask<ErrorOr<TNext>> ThenAsync<T, TNext>(this ValueTask<ErrorOr<T>> task, ...)

//    #endregion

//    #region Interoperability: NullOr -> ErrorOr

//    /// <summary>
//    /// Converts a NullOr&lt;TValue&gt; to an ErrorOr&lt;TValue&gt;.
//    /// If the NullOr is 'None', it creates an error result using the provided error.
//    /// </summary>
//    /// <param name="nullOr">The NullOr object to convert.</param>
//    /// <param name="error">The error to use if the value is 'None'.</param>
//    /// <returns>A successful ErrorOr if the NullOr has a value; otherwise, a failed ErrorOr with the provided error.</returns>
//    /// <example>
//    /// <code>
//    /// NullOr&lt;Customer&gt; maybeCustomer = _customerRepo.Find(id);
//    /// ErrorOr&lt;Customer&gt; customerResult = maybeCustomer.ToErrorOr(Errors.General.NotFound("Customer", id));
//    /// </code>
//    /// </example>
//    public static ErrorOr<TValue> ToErrorOr<TValue>(this NullOr<TValue> nullOr, Error error) {
//        return nullOr.HasValue ? nullOr.Value : error;
//    }

//    /// <summary>
//    /// Converts a NullOr&lt;TValue&gt; to an ErrorOr&lt;TValue&gt; using a lazy error factory.
//    /// This improves performance by only creating the error object when it's needed.
//    /// </summary>
//    /// <param name="nullOr">The NullOr object to convert.</param>
//    /// <param name="errorFactory">A function that produces an error if the value is 'None'.</param>
//    public static ErrorOr<TValue> ToErrorOr<TValue>(this NullOr<TValue> nullOr, Func<Error> errorFactory) {
//        return nullOr.HasValue ? nullOr.Value : errorFactory();
//    }

//    #endregion
//}