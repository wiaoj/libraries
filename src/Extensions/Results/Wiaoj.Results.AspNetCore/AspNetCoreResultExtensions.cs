using Wiaoj.Results;
using Wiaoj.Results.AspNetCore;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Http;
#pragma warning restore IDE0130
public static class AspNetCoreResultExtensions {
    /// <summary>
    /// Converts a <see cref="Wiaoj.Results.IResult"/> to an ASP.NET Core <see cref="IResult"/> (Problem Details).
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <exception cref="InvalidOperationException">Thrown when the result represents success.</exception>
    public static IResult ToProblemDetails(this Wiaoj.Results.IResult result) {
        if(result.IsSuccess)
            throw new InvalidOperationException("Cannot convert a successful result to ProblemDetails.");

        Error firstError = result.FirstError;

        // Merkezi mapper'dan kodu alıyoruz (Tamamen özelleştirilebilir!)
        int statusCode = ErrorMapper.GetStatusCode(firstError.Type.Name);

        Dictionary<string, object?> extensions = firstError.Metadata != null
            ? new Dictionary<string, object?>(firstError.Metadata)
            : [];

        if(result.Errors.Count > 1) {
            extensions["Errors"] = result.Errors.Select(e => new {
                e.Code,
                e.Description,
                Type = e.Type.Name
            });
        }

        return Results.Problem(
            statusCode: statusCode,
            title: firstError.Code,
            detail: firstError.Description,
            extensions: extensions
        );
    }
}