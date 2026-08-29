using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Contracts;
using Wiaoj.Results.AspNetCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Results;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extensions for converting <see cref="Error"/> and <see cref="IResult"/> to RFC 7807 <see cref="ProblemDetails"/>.
/// </summary>
public static class ResultProblemDetailsExtensions {

    /// <summary>
    /// Converts a single <see cref="Error"/> into a <see cref="ProblemDetails"/> instance.
    /// </summary>
    [Pure]
    public static ProblemDetails ToProblemDetails(this Error error, string? instance = null) {
        ProblemDetails problem = new() {
            Status = error.Type.ToStatusCode(),
            Title = error.Code,
            Detail = error.Description,
            Instance = instance,
            Type = $"https://httpstatuses.io/{error.Type.ToStatusCode()}"
        };

        if(error.Metadata is not null && error.Metadata.Count > 0) {
            foreach(var (key, value) in error.Metadata) {
                problem.Extensions[key] = value;
            }
        }

        return problem;
    }

    /// <summary>
    /// Converts a failed <see cref="IResult"/> into a <see cref="ProblemDetails"/> instance.
    /// </summary>
    [Pure]
    public static ProblemDetails ToProblemDetails(this IResult result, string? instance = null) {
        if(result.IsSuccess)
            throw new InvalidOperationException("Cannot generate ProblemDetails from a successful result.");

        Error firstError = result.FirstError;
        ProblemDetails problem = firstError.ToProblemDetails(instance);

        if(result.Errors.Count > 1) {
            problem.Extensions["errors"] = result.Errors.Select(e => new {
                code = e.Code,
                description = e.Description,
                type = e.Type.Name,
                metadata = e.Metadata
            }).ToArray();
        }

        return problem;
    }
}