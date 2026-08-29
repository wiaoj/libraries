using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Wiaoj.Results.AspNetCore.Internal;

/// <summary>
/// An internal endpoint filter that automatically unwraps returned <see cref="IResult"/> and <see cref="Result{TValue}"/>
/// instances into standard ASP.NET Core HTTP responses.
/// </summary>
internal sealed class ResultEndpointFilter : IEndpointFilter {

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        object? result = await next(context).ConfigureAwait(false);

        if(result is null)
            return null;

        if(result is IResult wiaojResult) {
            if(wiaojResult.IsFailure) {
                ProblemDetails problem = wiaojResult.ToProblemDetails(context.HttpContext.Request.Path);
                return TypedResults.Problem(problem);
            }

            Type resultType = result.GetType();
            if(resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>)) {
                Type valueType = resultType.GetGenericArguments()[0];
                if(valueType == typeof(Success)) {
                    return TypedResults.NoContent();
                }

                object? value = resultType.GetProperty(nameof(Result<>.Value))?.GetValue(result);
                return TypedResults.Ok(value);
            }

            return TypedResults.NoContent();
        }

        return result;
    }
}