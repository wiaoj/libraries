using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Contracts;
using Wiaoj.Results.AspNetCore;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Results;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extensions for converting <see cref="Error"/> and <see cref="IResult"/> to RFC 9457 <see cref="ProblemDetails"/>.
/// </summary>
/// <remarks>
/// The member roles follow RFC 9457: <c>type</c> identifies the problem type and belongs to whoever produced it
/// (see <see cref="ResultProblemDetailsProjection.TypeNamespace"/>), <c>title</c> is a short human summary that
/// repeats for every occurrence of that type, and the machine-readable <see cref="Error.Code"/> travels in the
/// <c>code</c> extension member. Before 0.4.0 the code sat in <c>title</c> and <c>type</c> was a
/// third-party status site — the two roles swapped — which is what
/// <see cref="ProblemTypeStyle.HttpStatusIo"/> remains available to reproduce.
/// </remarks>
public static class ResultProblemDetailsExtensions {

    /// <summary>
    /// The extension member carrying <see cref="Error.Code"/> verbatim, so a consumer can act on the machine key
    /// without parsing a human field.
    /// </summary>
    public const string CodeExtensionKey = "code";

    /// <summary>
    /// The extension member carrying the errors past the first one of a multi-error result.
    /// </summary>
    /// <remarks>
    /// Named <c>violations</c> rather than <c>errors</c>: <c>errors[]</c> is already bound to a
    /// <c>{pointer, message}</c> shape by the validation surface in this ecosystem, and one key with two schemas is
    /// a consumer trap rather than a compatibility win.
    /// </remarks>
    public const string ViolationsExtensionKey = "violations";

    /// <summary>
    /// Converts a single <see cref="Error"/> into a <see cref="ProblemDetails"/> instance.
    /// </summary>
    [Pure]
    public static ProblemDetails ToProblemDetails(this Error error, string? instance = null) {
        int statusCode = error.Type.ToStatusCode();

        ProblemDetails problem = new() {
            Status = statusCode,
            Title = ResultProblemDetailsProjection.BuildTitle(error),
            Detail = error.Description,
            Instance = instance,
            Type = ResultProblemDetailsProjection.BuildType(error, statusCode)
        };

        if(error.Metadata is not null && error.Metadata.Count > 0) {
            foreach(var (key, value) in error.Metadata) {
                problem.Extensions[key] = value;
            }
        }

        // Written after the metadata loop on purpose: a consumer that acts on the problem's identity has to be able
        // to count on this member meaning the error's code and nothing else.
        problem.Extensions[CodeExtensionKey] = error.Code;

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
            problem.Extensions[ViolationsExtensionKey] = result.Errors.Select(e => new {
                code = e.Code,
                description = e.Description,
                type = e.Type.Name,
                metadata = e.Metadata
            }).ToArray();
        }

        return problem;
    }
}