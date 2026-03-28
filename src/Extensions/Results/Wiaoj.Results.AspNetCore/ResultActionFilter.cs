using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Http;
public sealed class ResultActionFilter : IAsyncActionFilter {
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) {
        ActionExecutedContext executedContext = await next().ConfigureAwait(false);

        if(executedContext.Result is ObjectResult objectResult &&
            objectResult.Value is Wiaoj.Results.IResult res && res.IsFailure) {
            executedContext.Result = (IActionResult)res.ToProblemDetails();
        }
    }
}