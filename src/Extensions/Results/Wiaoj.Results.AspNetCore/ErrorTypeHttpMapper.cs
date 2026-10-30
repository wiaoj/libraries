using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Results;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// What <see cref="ErrorTypeHttpMapper.ToStatusCode(Wiaoj.Results.ErrorType)"/> does with an
/// <see cref="ErrorType"/> that has no HTTP status code mapping.
/// </summary>
public enum UnmappedErrorTypePolicy {

    /// <summary>
    /// Throw, so an un-mapped type is a build/test failure rather than an HTTP 500 in production.
    /// This is the default.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Return HTTP 500. This is the pre-0.4 behaviour and is only appropriate when the set of
    /// error types is not enumerable by the producer.
    /// </summary>
    InternalServerError = 1
}

/// <summary>
/// Provides mapping between <see cref="ErrorType"/> and standard HTTP status codes.
/// <para>
/// <see cref="ErrorType"/> is extensible, so the built-in table cannot be the whole answer. Producers
/// add their own types with <see cref="Register"/>, typically once at startup:
/// </para>
/// <code>
/// ErrorTypeHttpMapper.Register(AppErrorTypes.Locked, StatusCodes.Status423Locked);
/// </code>
/// A registration also overrides a built-in mapping for the same type name.
/// </summary>
public static class ErrorTypeHttpMapper {

    private static readonly ConcurrentDictionary<string, int> Registered = new(StringComparer.Ordinal);

    /// <summary>
    /// nginx's "Client Closed Request". Not IANA-registered and absent from <see cref="StatusCodes"/>,
    /// but it is the convention for a request the caller abandoned, and it keeps that outcome out of the
    /// 5xx budget the way 499 was designed to.
    /// </summary>
    private const int Status499ClientClosedRequest = 499;

    /// <summary>
    /// RFC 6585's "Precondition Required". Absent from <see cref="StatusCodes"/>, which never grew past the
    /// original RFC 7231 set, so the constant lives here.
    /// </summary>
    private const int Status428PreconditionRequired = 428;

    private static readonly Dictionary<string, int> BuiltIn = new(StringComparer.Ordinal) {
        [nameof(ErrorType.Validation)] = StatusCodes.Status400BadRequest,
        [nameof(ErrorType.Unauthorized)] = StatusCodes.Status401Unauthorized,
        [nameof(ErrorType.Forbidden)] = StatusCodes.Status403Forbidden,
        [nameof(ErrorType.NotFound)] = StatusCodes.Status404NotFound,
        [nameof(ErrorType.MethodNotAllowed)] = StatusCodes.Status405MethodNotAllowed,
        [nameof(ErrorType.Conflict)] = StatusCodes.Status409Conflict,
        [nameof(ErrorType.Gone)] = StatusCodes.Status410Gone,
        [nameof(ErrorType.PreconditionFailed)] = StatusCodes.Status412PreconditionFailed,
        [nameof(ErrorType.UnsupportedMediaType)] = StatusCodes.Status415UnsupportedMediaType,
        [nameof(ErrorType.UnprocessableEntity)] = StatusCodes.Status422UnprocessableEntity,
        [nameof(ErrorType.PreconditionRequired)] = Status428PreconditionRequired,
        [nameof(ErrorType.RateLimit)] = StatusCodes.Status429TooManyRequests,
        [nameof(ErrorType.Timeout)] = StatusCodes.Status408RequestTimeout,
        [nameof(ErrorType.Cancelled)] = Status499ClientClosedRequest,
        [nameof(ErrorType.NotImplemented)] = StatusCodes.Status501NotImplemented,
        [nameof(ErrorType.Unavailable)] = StatusCodes.Status503ServiceUnavailable,
        [nameof(ErrorType.Unexpected)] = StatusCodes.Status500InternalServerError,
        [nameof(ErrorType.Failure)] = StatusCodes.Status400BadRequest
    };

    /// <summary>
    /// Gets or sets the behaviour for an <see cref="ErrorType"/> with no mapping. Defaults to
    /// <see cref="UnmappedErrorTypePolicy.Throw"/>.
    /// </summary>
    public static UnmappedErrorTypePolicy OnUnmapped { get; set; } = UnmappedErrorTypePolicy.Throw;

    /// <summary>
    /// Maps <paramref name="errorType"/> to <paramref name="statusCode"/>, replacing any earlier
    /// registration or built-in mapping for the same type name.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="errorType"/> is the default value, so it carries no name to key on.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="statusCode"/> is not in the 100..599 range.
    /// </exception>
    public static void Register(ErrorType errorType, int statusCode) {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorType.Name);

        if(statusCode is < 100 or > 599)
            throw new ArgumentOutOfRangeException(
                nameof(statusCode),
                statusCode,
                $"HTTP status codes are in the range 100..599; {statusCode} cannot be registered for ErrorType '{errorType.Name}'.");

        Registered[errorType.Name] = statusCode;
    }

    /// <summary>
    /// Removes a producer registration, restoring the built-in mapping for that type name.
    /// </summary>
    /// <returns><see langword="true"/> if a registration was removed; otherwise, <see langword="false"/>.</returns>
    public static bool Unregister(ErrorType errorType) {
        return errorType.Name is not null && Registered.TryRemove(errorType.Name, out _);
    }

    /// <summary>
    /// Resolves the mapping for <paramref name="errorType"/> without applying
    /// <see cref="OnUnmapped"/>.
    /// </summary>
    /// <returns><see langword="true"/> when a registration or built-in mapping exists.</returns>
    [Pure]
    public static bool TryMap(ErrorType errorType, out int statusCode) {
        string? name = errorType.Name;

        if(name is not null
            && (Registered.TryGetValue(name, out statusCode) || BuiltIn.TryGetValue(name, out statusCode))) {
            return true;
        }

        statusCode = default;
        return false;
    }

    /// <summary>
    /// Maps the specified <see cref="ErrorType"/> to its corresponding HTTP status code.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the type has no mapping and <see cref="OnUnmapped"/> is
    /// <see cref="UnmappedErrorTypePolicy.Throw"/>.
    /// </exception>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToStatusCode(this ErrorType errorType) {
        if(TryMap(errorType, out int statusCode))
            return statusCode;

        if(OnUnmapped == UnmappedErrorTypePolicy.InternalServerError)
            return StatusCodes.Status500InternalServerError;

        ThrowUnmapped(errorType);
        return default;
    }

    /// <summary>
    /// Maps the specified <see cref="ErrorType"/> to its corresponding HTTP status code, falling back to
    /// <paramref name="fallbackStatusCode"/> when it has no mapping. The fallback is applied regardless of
    /// <see cref="OnUnmapped"/>, for call sites that would rather answer than fail.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToStatusCode(this ErrorType errorType, int fallbackStatusCode) {
        return TryMap(errorType, out int statusCode) ? statusCode : fallbackStatusCode;
    }

    [DoesNotReturn]
    private static void ThrowUnmapped(ErrorType errorType) {
        throw new InvalidOperationException(
            $"ErrorType '{errorType.NameOrDefault()}' has no HTTP status code mapping. "
            + $"Register one with ErrorTypeHttpMapper.Register(errorType, statusCode), "
            + $"or set ErrorTypeHttpMapper.OnUnmapped = {nameof(UnmappedErrorTypePolicy.InternalServerError)} "
            + "to keep answering with 500 instead of failing.");
    }
}

file static class ErrorTypeNameOrDefault {
    internal static string NameOrDefault(this ErrorType errorType) =>
        string.IsNullOrWhiteSpace(errorType.Name) ? "<uninitialized>" : errorType.Name!;
}
