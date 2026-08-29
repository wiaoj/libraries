using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Wiaoj.Results.AspNetCore;

/// <summary>
/// Extension methods for converting <see cref="Result{TValue}"/> into standard ASP.NET Core <see cref="Microsoft.AspNetCore.Http.IResult"/> responses.
/// </summary>
public static class ResultHttpExtensions {

    // ── ToHttpResult (Base Overloads) ─────────────────────────────────────────

    /// <summary>
    /// Converts a <see cref="Result{TValue}"/> into an HTTP response (<c>200 OK</c>, <c>204 NoContent</c>, or RFC 7807 <c>ProblemDetails</c>).
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Microsoft.AspNetCore.Http.IResult ToHttpResult<TValue>(
        this Result<TValue> result) {
        return ToHttpResultCore(result, null, null);
    }

    /// <summary>
    /// Converts a <see cref="Result{TValue}"/> into an HTTP response with a custom success status code.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Microsoft.AspNetCore.Http.IResult ToHttpResult<TValue>(
        this Result<TValue> result,
        int onSuccessStatusCode) {
        return ToHttpResultCore(result, onSuccessStatusCode, null);
    }

    /// <summary>
    /// Converts a <see cref="Result{TValue}"/> into an HTTP response specifying a request instance path for error diagnostics.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Microsoft.AspNetCore.Http.IResult ToHttpResult<TValue>(
        this Result<TValue> result,
        string instance) {
        return ToHttpResultCore(result, null, instance);
    }

    /// <summary>
    /// Converts a <see cref="Result{TValue}"/> into an HTTP response specifying both a custom success status code and request instance path.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Microsoft.AspNetCore.Http.IResult ToHttpResult<TValue>(
        this Result<TValue> result,
        int onSuccessStatusCode,
        string instance) {
        return ToHttpResultCore(result, onSuccessStatusCode, instance);
    }

    // ── ToHttpResult with Response Mapping (DTO Transformer) ──────────────────

    /// <summary>
    /// Maps the underlying value using <paramref name="mapper"/> when successful, returning an HTTP response.
    /// </summary>
    [Pure]
    public static Microsoft.AspNetCore.Http.IResult ToHttpResult<TValue, TResponse>(
        this Result<TValue> result,
        Func<TValue, TResponse> mapper) {
        ArgumentNullException.ThrowIfNull(mapper);

        if(result.IsSuccess)
            return TypedResults.Ok(mapper(result.Value));

        ProblemDetails problem = result.ToProblemDetails();
        return TypedResults.Problem(problem);
    }

    /// <summary>
    /// Maps the underlying value using <paramref name="mapper"/> when successful, returning an HTTP response with a custom status code.
    /// </summary>
    [Pure]
    public static Microsoft.AspNetCore.Http.IResult ToHttpResult<TValue, TResponse>(
        this Result<TValue> result,
        Func<TValue, TResponse> mapper,
        int onSuccessStatusCode) {
        ArgumentNullException.ThrowIfNull(mapper);

        if(result.IsSuccess)
            return new CustomStatusCodeHttpResult<TResponse>(mapper(result.Value), onSuccessStatusCode);

        ProblemDetails problem = result.ToProblemDetails();
        return TypedResults.Problem(problem);
    }

    // ── ToCreatedHttpResult (201 Created) ─────────────────────────────────────

    /// <summary>
    /// Converts a successful <see cref="Result{TValue}"/> to a <c>201 Created</c> HTTP response with a location string URI.
    /// </summary>
    [Pure]
    public static Microsoft.AspNetCore.Http.IResult ToCreatedHttpResult<TValue>(
        this Result<TValue> result,
        string uri) {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        if(result.IsSuccess)
            return TypedResults.Created(uri, result.Value);

        ProblemDetails problem = result.ToProblemDetails();
        return TypedResults.Problem(problem);
    }

    /// <summary>
    /// Converts a successful <see cref="Result{TValue}"/> to a <c>201 Created</c> HTTP response with a location <see cref="Uri"/>.
    /// </summary>
    [Pure]
    public static Microsoft.AspNetCore.Http.IResult ToCreatedHttpResult<TValue>(
        this Result<TValue> result,
        Uri uri) {
        ArgumentNullException.ThrowIfNull(uri);

        if(result.IsSuccess)
            return TypedResults.Created(uri, result.Value);

        ProblemDetails problem = result.ToProblemDetails();
        return TypedResults.Problem(problem);
    }

    /// <summary>
    /// Converts a successful <see cref="Result{TValue}"/> to a <c>201 Created</c> HTTP response specifying a request instance path.
    /// </summary>
    [Pure]
    public static Microsoft.AspNetCore.Http.IResult ToCreatedHttpResult<TValue>(
        this Result<TValue> result,
        string uri,
        string instance) {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        if(result.IsSuccess)
            return TypedResults.Created(uri, result.Value);

        ProblemDetails problem = result.ToProblemDetails(instance);
        return TypedResults.Problem(problem);
    }

    // ── ToAcceptedHttpResult (202 Accepted) ───────────────────────────────────

    /// <summary>
    /// Converts a successful <see cref="Result{TValue}"/> to a <c>202 Accepted</c> HTTP response.
    /// </summary>
    [Pure]
    public static Microsoft.AspNetCore.Http.IResult ToAcceptedHttpResult<TValue>(
        this Result<TValue> result) {
        if(result.IsSuccess)
            return TypedResults.Accepted(string.Empty, result.Value);

        ProblemDetails problem = result.ToProblemDetails();
        return TypedResults.Problem(problem);
    }

    /// <summary>
    /// Converts a successful <see cref="Result{TValue}"/> to a <c>202 Accepted</c> HTTP response with a location status URI.
    /// </summary>
    [Pure]
    public static Microsoft.AspNetCore.Http.IResult ToAcceptedHttpResult<TValue>(
        this Result<TValue> result,
        string uri) {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        if(result.IsSuccess)
            return TypedResults.Accepted(uri, result.Value);

        ProblemDetails problem = result.ToProblemDetails();
        return TypedResults.Problem(problem);
    }

    // ── Task Asynchronous Extensions ──────────────────────────────────────────

    /// <summary>Awaits <paramref name="task"/> and converts it to an HTTP response.</summary>
    [Pure]
    public static async Task<Microsoft.AspNetCore.Http.IResult> ToHttpResultAsync<TValue>(
        this Task<Result<TValue>> task) {
        ArgumentNullException.ThrowIfNull(task);
        Result<TValue> result = await task.ConfigureAwait(false);
        return result.ToHttpResult();
    }

    /// <summary>Awaits <paramref name="task"/> and converts it to an HTTP response with a custom status code.</summary>
    [Pure]
    public static async Task<Microsoft.AspNetCore.Http.IResult> ToHttpResultAsync<TValue>(
        this Task<Result<TValue>> task,
        int onSuccessStatusCode) {
        ArgumentNullException.ThrowIfNull(task);
        Result<TValue> result = await task.ConfigureAwait(false);
        return result.ToHttpResult(onSuccessStatusCode);
    }

    /// <summary>Awaits <paramref name="task"/> and converts it to a <c>201 Created</c> HTTP response.</summary>
    [Pure]
    public static async Task<Microsoft.AspNetCore.Http.IResult> ToCreatedHttpResultAsync<TValue>(
        this Task<Result<TValue>> task,
        string uri) {
        ArgumentNullException.ThrowIfNull(task);
        Result<TValue> result = await task.ConfigureAwait(false);
        return result.ToCreatedHttpResult(uri);
    }

    /// <summary>Awaits <paramref name="task"/> and converts it to an HTTP response mapping the success value.</summary>
    [Pure]
    public static async Task<Microsoft.AspNetCore.Http.IResult> ToHttpResultAsync<TValue, TResponse>(
        this Task<Result<TValue>> task,
        Func<TValue, TResponse> mapper) {
        ArgumentNullException.ThrowIfNull(task);
        Result<TValue> result = await task.ConfigureAwait(false);
        return result.ToHttpResult(mapper);
    }

    // ── ValueTask Asynchronous Extensions ─────────────────────────────────────

    /// <summary>Awaits <paramref name="valueTask"/> and converts it to an HTTP response.</summary>
    [Pure]
    public static async ValueTask<Microsoft.AspNetCore.Http.IResult> ToHttpResultAsync<TValue>(
        this ValueTask<Result<TValue>> valueTask) {
        Result<TValue> result = await valueTask.ConfigureAwait(false);
        return result.ToHttpResult();
    }

    /// <summary>Awaits <paramref name="valueTask"/> and converts it to an HTTP response with a custom status code.</summary>
    [Pure]
    public static async ValueTask<Microsoft.AspNetCore.Http.IResult> ToHttpResultAsync<TValue>(
        this ValueTask<Result<TValue>> valueTask,
        int onSuccessStatusCode) {
        Result<TValue> result = await valueTask.ConfigureAwait(false);
        return result.ToHttpResult(onSuccessStatusCode);
    }

    /// <summary>Awaits <paramref name="valueTask"/> and converts it to a <c>201 Created</c> HTTP response.</summary>
    [Pure]
    public static async ValueTask<Microsoft.AspNetCore.Http.IResult> ToCreatedHttpResultAsync<TValue>(
        this ValueTask<Result<TValue>> valueTask,
        string uri) {
        Result<TValue> result = await valueTask.ConfigureAwait(false);
        return result.ToCreatedHttpResult(uri);
    }

    // ── Core Engine ───────────────────────────────────────────────────────────

    private static Microsoft.AspNetCore.Http.IResult ToHttpResultCore<TValue>(
        Result<TValue> result,
        int? onSuccessStatusCode,
        string? instance) {
        if(result.IsSuccess) {
            if(typeof(TValue) == typeof(Success)) {
                return onSuccessStatusCode.HasValue
                    ? TypedResults.StatusCode(onSuccessStatusCode.Value)
                    : TypedResults.NoContent();
            }

            if(onSuccessStatusCode.HasValue) {
                return onSuccessStatusCode.Value switch {
                    StatusCodes.Status200OK => TypedResults.Ok(result.Value),
                    StatusCodes.Status201Created => TypedResults.Created(string.Empty, result.Value),
                    StatusCodes.Status202Accepted => TypedResults.Accepted(string.Empty, result.Value),
                    StatusCodes.Status204NoContent => TypedResults.NoContent(),
                    _ => new CustomStatusCodeHttpResult<TValue>(result.Value, onSuccessStatusCode.Value)
                };
            }

            return TypedResults.Ok(result.Value);
        }

        ProblemDetails problem = result.ToProblemDetails(instance);
        return TypedResults.Problem(problem);
    }

    // ── Internal AOT-Safe Custom Status Code Result ───────────────────────────

    private sealed class CustomStatusCodeHttpResult<T>(T? value, int statusCode) : Microsoft.AspNetCore.Http.IResult {
        public Task ExecuteAsync(HttpContext httpContext) {
            httpContext.Response.StatusCode = statusCode;
            return httpContext.Response.WriteAsJsonAsync(value);
        }
    }
}