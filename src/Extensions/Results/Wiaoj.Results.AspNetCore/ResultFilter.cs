#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Http;
public sealed class ResultFilter : IEndpointFilter {
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        object? result = await next(context).ConfigureAwait(false);

        if(result is Wiaoj.Results.IResult res && res.IsFailure) {
            return res.ToProblemDetails();
        }
        return result;
    }
}
