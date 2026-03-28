using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

#pragma warning disable IDE0130
namespace Wiaoj.Results.AspNetCore;
/// <summary>
/// Provides a centralized mechanism to map <see cref="ErrorType"/> names to HTTP status codes.
/// <para>
/// By default, common <see cref="ErrorType"/> values are pre-mapped to appropriate status codes.
/// Use <see cref="Map(string, int)"/> to override or extend these mappings for domain-specific error types.
/// </para>
/// </summary>
public static class ErrorMapper {
    private static readonly ConcurrentDictionary<string, int> _statusCodes = new(StringComparer.OrdinalIgnoreCase); 
    
    static ErrorMapper() {
        ResetToDefaults();
    }

    /// <summary>
    /// Clears all existing mappings, including default ones. 
    /// Use this if you want full control over your status code mappings.
    /// </summary>
    public static void Clear() {
        _statusCodes.Clear();
    }

    /// <summary>
    /// Removes a mapping for a specific <see cref="ErrorType"/> name.
    /// </summary>
    public static void Unmap(string errorTypeName) {
        _statusCodes.TryRemove(errorTypeName, out _);
    }

    /// <summary>
    /// Registers or updates a mapping between an <see cref="ErrorType"/> name and an HTTP status code.
    /// </summary>
    public static void Map(string errorTypeName, int statusCode) {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorTypeName);
        _statusCodes[errorTypeName] = statusCode;
    }

    /// <summary>
    /// Clears all custom mappings and resets the mapper to its default state.
    /// </summary>
    public static void ResetToDefaults() {
        _statusCodes.Clear();
        _statusCodes[nameof(ErrorType.Failure)] = StatusCodes.Status500InternalServerError;
        _statusCodes[nameof(ErrorType.Validation)] = StatusCodes.Status400BadRequest;
        _statusCodes[nameof(ErrorType.NotFound)] = StatusCodes.Status404NotFound;
        _statusCodes[nameof(ErrorType.Conflict)] = StatusCodes.Status409Conflict;
        _statusCodes[nameof(ErrorType.Unauthorized)] = StatusCodes.Status401Unauthorized;
        _statusCodes[nameof(ErrorType.Forbidden)] = StatusCodes.Status403Forbidden;
        _statusCodes[nameof(ErrorType.Unexpected)] = StatusCodes.Status500InternalServerError;
        _statusCodes[nameof(ErrorType.RateLimit)] = StatusCodes.Status429TooManyRequests;
        _statusCodes[nameof(ErrorType.Timeout)] = StatusCodes.Status408RequestTimeout; 
        _statusCodes[nameof(ErrorType.Unavailable)] = StatusCodes.Status503ServiceUnavailable;
        _statusCodes[nameof(ErrorType.Gone)] = StatusCodes.Status410Gone;
        _statusCodes[nameof(ErrorType.UnprocessableEntity)] = StatusCodes.Status422UnprocessableEntity;
    }


    /// <summary>
    /// Resolves the HTTP status code for the given <see cref="ErrorType"/> name.
    /// Returns <see cref="StatusCodes.Status500InternalServerError"/> if no mapping is found.
    /// </summary>
    /// <param name="errorTypeName">The <see cref="ErrorType.Name"/> to resolve.</param>
    /// <returns>The associated HTTP status code.</returns>
    internal static int GetStatusCode(string errorTypeName) {
        return _statusCodes.TryGetValue(errorTypeName, out int code)
            ? code
            : StatusCodes.Status500InternalServerError;
    }
}