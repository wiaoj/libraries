using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Wiaoj.Webhooks;

/// <summary>
/// Represents a single, historical delivery attempt for a webhook, capturing its outcome
/// and timing for observability and retry-decision purposes.
/// </summary>
/// <remarks>
/// A new <see cref="WebhookDeliveryAttempt"/> is recorded every time a <see cref="WebhookDeliveryJob"/>
/// is handed to an <see cref="IWebhookDeliverer"/>, regardless of whether that attempt succeeded.
/// The full history for a given delivery is exposed via <see cref="WebhookDeliveryContext.AttemptHistory"/>.
/// </remarks>
public sealed record WebhookDeliveryAttempt {

    /// <summary>
    /// Gets the identifier of the endpoint this attempt was made against.
    /// </summary>
    public WebhookEndpointId EndpointId { get; }

    /// <summary>
    /// Gets the one-based sequence number of this attempt within the delivery's retry history.
    /// </summary>
    /// <remarks>
    /// The first attempt is always <c>1</c>; each subsequent retry increments this value by one.
    /// </remarks>
    public int AttemptNumber { get; }

    /// <summary>
    /// Gets the moment at which this attempt was made.
    /// </summary>
    public UnixTimestamp AttemptedAt { get; }

    /// <summary>
    /// Gets the wall-clock time the attempt took, from request start to response or failure.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the result produced by the <see cref="IWebhookDeliverer"/> for this attempt.
    /// </summary>
    public WebhookDeliveryResult Result { get; }

    /// <summary>
    /// Gets a value indicating whether this attempt succeeded.
    /// </summary>
    /// <remarks>
    /// Convenience accessor over <see cref="Result"/>; does not carry independent state.
    /// </remarks>
    [JsonIgnore]
    public bool IsSuccess {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Result.IsSuccess;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookDeliveryAttempt"/> record.
    /// </summary>
    /// <param name="endpointId">The identifier of the endpoint this attempt was made against.</param>
    /// <param name="attemptNumber">The one-based sequence number of this attempt.</param>
    /// <param name="attemptedAt">The moment at which this attempt was made.</param>
    /// <param name="duration">The wall-clock time the attempt took to complete.</param>
    /// <param name="result">The result produced by the deliverer for this attempt.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="attemptNumber"/> is less than <c>1</c>, or when <paramref name="duration"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="result"/> is <see langword="null"/>.
    /// </exception>
    public WebhookDeliveryAttempt(
        WebhookEndpointId endpointId,
        int attemptNumber,
        UnixTimestamp attemptedAt,
        TimeSpan duration,
        WebhookDeliveryResult result) {
        Preca.ThrowIfLessThan(attemptNumber, 1);
        Preca.ThrowIfNegative(duration);
        Preca.ThrowIfNull(result);

        this.EndpointId = endpointId;
        this.AttemptNumber = attemptNumber;
        this.AttemptedAt = attemptedAt;
        this.Duration = duration;
        this.Result = result;
    }
}