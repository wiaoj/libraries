using Microsoft.AspNetCore.Http;

namespace Wiaoj.Webhooks.AspNetCore.Diagnostics;

/// <summary>
/// Centralized, Native AOT-compliant HTTP results and RFC 9457 Problem Details for inbound webhook endpoints.
/// </summary>
public static class WebhookReceiverResponses {
    /// <summary>Canonical 200 OK result for successfully processed or safely deduplicated webhooks.</summary>
    public static readonly IResult Ok = TypedResults.Ok();

    /// <summary>Canonical 200 OK result acknowledging inbound ping healthcheck probes.</summary>
    public static readonly IResult Pong = TypedResults.Ok();

    /// <summary>RFC 9457 Problem Details for empty or missing request bodies (400 Bad Request).</summary>
    public static IResult InvalidBody(string? path = null) {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid Webhook Body",
            detail: "The webhook request body was empty or contained invalid payload data.",
            instance: path,
            type: "https://tools.ietf.org/html/rfc9457");
    }

    /// <summary>RFC 9457 Problem Details for payload size exceeding limits (413 Payload Too Large).</summary>
    public static IResult PayloadTooLarge(int maxBytes, string? path = null) {
        return Results.Problem(
            statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "Webhook Payload Too Large",
            detail: $"The request body exceeded the maximum allowable size of {maxBytes} bytes.",
            instance: path,
            type: "https://tools.ietf.org/html/rfc9457");
    }

    /// <summary>RFC 9457 Problem Details for deserialization failures (400 Bad Request).</summary>
    public static IResult DeserializationFailed(string eventName, string? path = null) {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Webhook Deserialization Failed",
            detail: $"Could not deserialize payload into the target contract for event '{eventName}'.",
            instance: path,
            type: "https://tools.ietf.org/html/rfc9457");
    }

    /// <summary>RFC 9457 Problem Details for missing or invalid cryptographic signatures (401 Unauthorized).</summary>
    public static IResult UnauthorizedSignature(string? path = null) {
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Webhook Signature Verification Failed",
            detail: "The cryptographic signature was missing, expired, or invalid for this payload.",
            instance: path,
            type: "https://tools.ietf.org/html/rfc9457");
    }

    /// <summary>RFC 9457 Problem Details for loop detection / hop count breach (422 Unprocessable Entity).</summary>
    public static IResult LoopDetected(int maxHops, int currentHops, string? path = null) {
        return Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Webhook Execution Loop Detected",
            detail: $"The request exceeded the maximum allowable hop count limit of {maxHops} (Current: {currentHops}).",
            instance: path,
            type: "https://tools.ietf.org/html/rfc9457");
    }
}