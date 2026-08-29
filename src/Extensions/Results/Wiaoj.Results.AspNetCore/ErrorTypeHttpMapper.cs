using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Results;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides mapping between <see cref="ErrorType"/> and standard HTTP status codes.
/// </summary>
public static class ErrorTypeHttpMapper {

    /// <summary>
    /// Maps the specified <see cref="ErrorType"/> to its corresponding HTTP status code.
    /// </summary>
    /// <param name="errorType">The error type to map.</param>
    /// <returns>The corresponding HTTP status code.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToStatusCode(this ErrorType errorType) {
        if(errorType == ErrorType.Validation) return StatusCodes.Status400BadRequest;
        if(errorType == ErrorType.Unauthorized) return StatusCodes.Status401Unauthorized;
        if(errorType == ErrorType.Forbidden) return StatusCodes.Status403Forbidden;
        if(errorType == ErrorType.NotFound) return StatusCodes.Status404NotFound;
        if(errorType == ErrorType.Conflict) return StatusCodes.Status409Conflict;
        if(errorType == ErrorType.Gone) return StatusCodes.Status410Gone;
        if(errorType == ErrorType.UnprocessableEntity) return StatusCodes.Status422UnprocessableEntity;
        if(errorType == ErrorType.RateLimit) return StatusCodes.Status429TooManyRequests;
        if(errorType == ErrorType.Timeout) return StatusCodes.Status504GatewayTimeout;
        if(errorType == ErrorType.Unavailable) return StatusCodes.Status503ServiceUnavailable;
        if(errorType == ErrorType.Unexpected) return StatusCodes.Status500InternalServerError;
        if(errorType == ErrorType.Failure) return StatusCodes.Status500InternalServerError;

        return StatusCodes.Status500InternalServerError;
    }
}