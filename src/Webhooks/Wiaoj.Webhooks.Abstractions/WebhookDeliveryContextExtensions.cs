using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Wiaoj.Webhooks;

/// <summary>
/// High-performance, strongly-typed extension methods for <see cref="WebhookDeliveryContext"/>
/// eliminating primitive dictionary access, magic string keys, and manual type casting.
/// </summary>
public static class WebhookDeliveryContextExtensions {

    // ────────────────────────────────────────────────────────────────────────
    // 1. CONVENIENCE SHORTCUT PROPERTIES
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the unique identifier of the target endpoint.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>The <see cref="WebhookEndpointId"/> associated with this context.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WebhookEndpointId GetEndpointId(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.Endpoint.Id;
    }

    /// <summary>
    /// Gets the one-based attempt number currently being executed (History count + 1).
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>The one-based attempt number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCurrentAttemptNumber(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.AttemptHistory.Count + 1;
    }

    /// <summary>
    /// Gets a value indicating whether the current delivery attempt is the very first attempt (Attempt #1).
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns><see langword="true"/> if this is the initial delivery attempt; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFirstAttempt(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.AttemptHistory.Count == 0;
    }

    /// <summary>
    /// Gets a value indicating whether the current delivery attempt is a subsequent retry attempt (Attempt #2+).
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns><see langword="true"/> if this is a retry attempt; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRetry(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.AttemptHistory.Count > 0;
    }

    /// <summary>
    /// Gets the most recent prior delivery attempt from history, or <see langword="null"/> if this is the first attempt.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>The latest <see cref="WebhookDeliveryAttempt"/> if available; otherwise, <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WebhookDeliveryAttempt? GetLastAttempt(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.AttemptHistory.Count > 0
            ? context.AttemptHistory[^1]
            : null;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. DELIVERY RESULT ACCESSORS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores the delivery outcome result in the context.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="result">The outcome result produced by a deliverer or short-circuiting middleware.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetResult(this WebhookDeliveryContext context, WebhookDeliveryResult result) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(result);
        context.Items[WebhookDeliveryContextItemKeys.Result] = result;
    }

    /// <summary>
    /// Retrieves the delivery outcome result recorded in the context, if set.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>The <see cref="WebhookDeliveryResult"/> instance, or <see langword="null"/> if not yet set.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WebhookDeliveryResult? GetResult(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.Items.TryGetValue(WebhookDeliveryContextItemKeys.Result, out object? raw)
            && raw is WebhookDeliveryResult result
                ? result
                : null;
    }

    /// <summary>
    /// Tries to retrieve the delivery outcome result recorded in the context.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="result">When this method returns, contains the result if found.</param>
    /// <returns><see langword="true"/> if a result was found; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetResult(this WebhookDeliveryContext context, [NotNullWhen(true)] out WebhookDeliveryResult? result) {
        result = context.GetResult();
        return result is not null;
    }

    /// <summary>
    /// Tries to retrieve the delivery outcome result recorded in the context, returning <see langword="true"/>
    /// only if a result is present and represents a successful outcome (<see cref="WebhookDeliveryResult.IsSuccess"/> is <see langword="true"/>).
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="result">When this method returns, contains the successful result if found and valid; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a successful delivery result was recorded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetSuccessResult(
        this WebhookDeliveryContext context,
        [NotNullWhen(true)] out WebhookDeliveryResult? result) {
        Preca.ThrowIfNull(context);

        result = context.GetResult();
        if(result is not null && result.IsSuccess) {
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Determines whether a delivery outcome result has been recorded in the context and represents a successful outcome (<see cref="WebhookDeliveryResult.IsSuccess"/> is <see langword="true"/>).
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns><see langword="true"/> if a result is present and successful; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasSuccessResult(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.GetResult()?.IsSuccess is true;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. DEAD-LETTERING ACCESSORS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Marks the context as permanently dead-lettered, signaling to the persistent store that no further retries should occur.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MarkDeadLettered(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        context.Items[WebhookDeliveryContextItemKeys.IsDeadLettered] = true;
    }

    /// <summary>
    /// Checks whether the delivery attempt was flagged as dead-lettered.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns><see langword="true"/> if marked as dead-lettered; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDeadLettered(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.Items.TryGetValue(WebhookDeliveryContextItemKeys.IsDeadLettered, out object? raw)
            && raw is true;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. HTTP HEADERS ACCESSORS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves the outbound HTTP custom headers dictionary from the context, or an empty dictionary if none set.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>A read-only view of configured outbound HTTP headers.</returns>
    public static IReadOnlyDictionary<string, string> GetHeaders(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.Items.TryGetValue(WebhookDeliveryContextItemKeys.Headers, out object? raw)
            && raw is IReadOnlyDictionary<string, string> headers
                ? headers
                : EmptyHeadersDictionary.Instance;
    }

    /// <summary>
    /// Gets the mutable outbound HTTP headers dictionary from the context, creating one if it does not yet exist.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>The mutable dictionary of outbound HTTP headers.</returns>
    public static IDictionary<string, string> GetOrCreateHeaders(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        if(!context.Items.TryGetValue(WebhookDeliveryContextItemKeys.Headers, out object? raw) || raw is not IDictionary<string, string> headers) {
            headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            context.Items[WebhookDeliveryContextItemKeys.Headers] = headers;
        }
        return headers;
    }

    /// <summary>
    /// Adds or updates an outbound HTTP header in the delivery context.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="name">The HTTP header name.</param>
    /// <param name="value">The HTTP header value.</param>
    public static void SetHeader(this WebhookDeliveryContext context, string name, string value) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNull(value);

        IDictionary<string, string> headers = context.GetOrCreateHeaders();
        headers[name] = value;
    }

    /// <summary>
    /// Checks whether an outbound HTTP header is present in the context.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="name">The HTTP header name to check.</param>
    /// <returns><see langword="true"/> if the header exists; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasHeader(this WebhookDeliveryContext context, string name) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNullOrWhiteSpace(name);

        return context.GetHeaders().ContainsKey(name);
    }

    /// <summary>
    /// Retrieves the value of a specific outbound HTTP header, or <see langword="null"/> if not present.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="name">The HTTP header name.</param>
    /// <returns>The header value if found; otherwise, <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetHeader(this WebhookDeliveryContext context, string name) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNullOrWhiteSpace(name);

        return context.GetHeaders().TryGetValue(name, out string? value) ? value : null;
    }

    /// <summary>
    /// Tries to retrieve the value of a specific outbound HTTP header.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="name">The HTTP header name.</param>
    /// <param name="value">When this method returns, contains the header value if found.</param>
    /// <returns><see langword="true"/> if the header was found; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetHeader(
        this WebhookDeliveryContext context,
        string name,
        [NotNullWhen(true)] out string? value) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNullOrWhiteSpace(name);

        return context.GetHeaders().TryGetValue(name, out value);
    }

    /// <summary>
    /// Removes an outbound HTTP header from the delivery context if present.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="name">The HTTP header name to remove.</param>
    /// <returns><see langword="true"/> if the header was found and removed; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool RemoveHeader(this WebhookDeliveryContext context, string name) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNullOrWhiteSpace(name);

        if(context.Items.TryGetValue(WebhookDeliveryContextItemKeys.Headers, out object? raw) && raw is IDictionary<string, string> headers) {
            return headers.Remove(name);
        }

        return false;
    }

    /// <summary>
    /// Adds or updates multiple outbound HTTP headers simultaneously.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="headers">The collection of headers to add.</param>
    public static void SetHeaders(this WebhookDeliveryContext context, IEnumerable<KeyValuePair<string, string>> headers) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(headers);

        IDictionary<string, string> target = context.GetOrCreateHeaders();
        foreach(KeyValuePair<string, string> kvp in headers) {
            target[kvp.Key] = kvp.Value;
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. ZERO-ALLOCATION PAYLOAD ACCESSORS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a direct, zero-allocation <see cref="ReadOnlySpan{Char}"/> view over the pre-serialized payload.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>A character span of the serialized payload.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> GetPayloadSpan(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.SerializedPayload.AsSpan();
    }

    /// <summary>
    /// Gets the exact UTF-8 byte count of the serialized payload without allocating a byte array.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>The number of UTF-8 encoded bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPayloadByteCount(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return Encoding.UTF8.GetByteCount(context.SerializedPayload);
    }

    /// <summary>
    /// Writes the serialized payload as UTF-8 bytes directly into the destination span without heap allocations.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="destination">The destination byte buffer.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
    /// <returns><see langword="true"/> if the destination was large enough; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryWritePayloadBytes(this WebhookDeliveryContext context, Span<byte> destination, out int bytesWritten) {
        Preca.ThrowIfNull(context);
        return Encoding.UTF8.TryGetBytes(context.SerializedPayload.AsSpan(), destination, out bytesWritten);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 6. SIGNATURE ACCESSORS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores the computed cryptographic signature in the delivery context.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="signature">The computed signature.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetSignature(this WebhookDeliveryContext context, WebhookSignature signature) {
        Preca.ThrowIfNull(context);
        context.Items[WebhookDeliveryContextItemKeys.Signature] = signature;
    }

    /// <summary>
    /// Retrieves the computed cryptographic signature from the context, if set.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>The <see cref="WebhookSignature"/> if found; otherwise, <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WebhookSignature? GetSignature(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.Items.TryGetValue(WebhookDeliveryContextItemKeys.Signature, out object? raw)
            && raw is WebhookSignature sig
                ? sig
                : null;
    }

    /// <summary>
    /// Tries to retrieve the computed cryptographic signature from the context.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="signature">When this method returns, contains the signature if computed.</param>
    /// <returns><see langword="true"/> if a signature is present; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetSignature(
        this WebhookDeliveryContext context,
        [NotNullWhen(true)] out WebhookSignature? signature) {
        signature = context.GetSignature();
        return signature is not null;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 7. EVENT ACCESSORS & TYPE GUARDS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the canonical wire-format event name (e.g., <c>"order.created"</c>).
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>The wire-format event name associated with this delivery attempt.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetEventName(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.EventType;
    }

    /// <summary>
    /// Determines whether the event being delivered matches the specified canonical wire-format name.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="eventName">The expected wire-format event name to compare against.</param>
    /// <returns><see langword="true"/> if the event name matches; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEvent(this WebhookDeliveryContext context, string eventName) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNullOrWhiteSpace(eventName);

        return string.Equals(context.EventType, eventName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the domain payload is of the specified strongly-typed event type.
    /// </summary>
    /// <typeparam name="TEvent">The expected event type.</typeparam>
    /// <param name="context">The delivery context.</param>
    /// <returns><see langword="true"/> if the underlying payload is an instance of <typeparamref name="TEvent"/>; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEvent<TEvent>(this WebhookDeliveryContext context) where TEvent : class, IWebhookEvent {
        Preca.ThrowIfNull(context);
        return context.Event is TEvent;
    }

    /// <summary>
    /// Attempts to safely cast the domain payload to the specified strongly-typed event type.
    /// </summary>
    /// <typeparam name="TEvent">The expected event type.</typeparam>
    /// <param name="context">The delivery context.</param>
    /// <param name="event">When this method returns, contains the typed event instance if compatible; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the payload was successfully cast to <typeparamref name="TEvent"/>; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetEvent<TEvent>(
        this WebhookDeliveryContext context,
        [NotNullWhen(true)] out TEvent? @event)
        where TEvent : class, IWebhookEvent {
        Preca.ThrowIfNull(context);

        if(context.Event is TEvent typedEvent) {
            @event = typedEvent;
            return true;
        }

        @event = null;
        return false;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 8. IDEMPOTENCY KEY ACCESSORS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores the generated idempotency key in the delivery context.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="key">The generated idempotency key.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetIdempotencyKey(this WebhookDeliveryContext context, IdempotencyKey key) {
        Preca.ThrowIfNull(context);
        context.Items[WebhookDeliveryContextItemKeys.IdempotencyKey] = key;
    }

    /// <summary>
    /// Retrieves the generated idempotency key from the context, or <see langword="null"/> if not yet generated.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>The <see cref="IdempotencyKey"/> if present; otherwise, <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IdempotencyKey? GetIdempotencyKey(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);
        return context.Items.TryGetValue(WebhookDeliveryContextItemKeys.IdempotencyKey, out object? raw)
            && raw is IdempotencyKey key
                ? key
                : null;
    }

    /// <summary>
    /// Tries to retrieve the generated idempotency key from the context.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="key">When this method returns, contains the key if found.</param>
    /// <returns><see langword="true"/> if the key was found; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetIdempotencyKey(this WebhookDeliveryContext context, out IdempotencyKey key) {
        Preca.ThrowIfNull(context);
        if(context.Items.TryGetValue(WebhookDeliveryContextItemKeys.IdempotencyKey, out object? raw) && raw is IdempotencyKey typedKey) {
            key = typedKey;
            return true;
        }

        key = default;
        return false;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 9. ATTEMPT HISTORY & STATUS CODE ANALYTICS
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Determines whether any prior delivery attempt for this job encountered the specified HTTP status code.
    /// Useful for retry policies checking if an endpoint previously returned <c>429 Too Many Requests</c>.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <param name="statusCode">The HTTP status code to search for (e.g. 429, 503).</param>
    /// <returns><see langword="true"/> if the status code was observed in attempt history; otherwise, <see langword="false"/>.</returns>
    public static bool HasEncounteredStatusCode(this WebhookDeliveryContext context, int statusCode) {
        Preca.ThrowIfNull(context);

        for(int i = 0; i < context.AttemptHistory.Count; i++) {
            WebhookDeliveryAttempt attempt = context.AttemptHistory[i];
            if(attempt.Result is WebhookDeliveryResult.TransientFailure tf && tf.StatusCode == statusCode) {
                return true;
            }
            if(attempt.Result is WebhookDeliveryResult.PermanentFailure pf && pf.StatusCode == statusCode) {
                return true;
            }
            if(attempt.Result is WebhookDeliveryResult.Delivered d && d.StatusCode == statusCode) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Calculates the cumulative execution duration spent across all prior delivery attempts.
    /// </summary>
    /// <param name="context">The delivery context.</param>
    /// <returns>The total wall-clock duration of all prior attempts.</returns>
    public static TimeSpan GetTotalPriorAttemptsDuration(this WebhookDeliveryContext context) {
        Preca.ThrowIfNull(context);

        TimeSpan total = TimeSpan.Zero;
        for(int i = 0; i < context.AttemptHistory.Count; i++) {
            total += context.AttemptHistory[i].Duration;
        }

        return total;
    }

    // ────────────────────────────────────────────────────────────────────────
    // 10. GENERIC SAFE ITEMS ACCESSORS & FACTORY
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores an arbitrary typed item in <see cref="WebhookDeliveryContext.Items"/>.
    /// </summary>
    /// <typeparam name="T">The type of the item being stored.</typeparam>
    /// <param name="context">The delivery context.</param>
    /// <param name="key">The dictionary key.</param>
    /// <param name="value">The item value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetItem<T>(this WebhookDeliveryContext context, string key, T value) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNullOrWhiteSpace(key);
        context.Items[key] = value;
    }

    /// <summary>
    /// Retrieves a typed item from <see cref="WebhookDeliveryContext.Items"/>.
    /// </summary>
    /// <typeparam name="T">The expected type of the item.</typeparam>
    /// <param name="context">The delivery context.</param>
    /// <param name="key">The dictionary key.</param>
    /// <returns>The item cast to <typeparamref name="T"/>, or <see langword="default"/> if not found or incompatible.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? GetItem<T>(this WebhookDeliveryContext context, string key) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNullOrWhiteSpace(key);
        return context.Items.TryGetValue(key, out object? raw) && raw is T typed
            ? typed
            : default;
    }

    /// <summary>
    /// Tries to retrieve a typed item from <see cref="WebhookDeliveryContext.Items"/>.
    /// </summary>
    /// <typeparam name="T">The expected type of the item.</typeparam>
    /// <param name="context">The delivery context.</param>
    /// <param name="key">The dictionary key.</param>
    /// <param name="value">When this method returns, contains the item if found.</param>
    /// <returns><see langword="true"/> if found and compatible with <typeparamref name="T"/>; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetItem<T>(this WebhookDeliveryContext context, string key, out T? value) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNullOrWhiteSpace(key);
        if(context.Items.TryGetValue(key, out object? raw) && raw is T typed) {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Retrieves an item from <see cref="WebhookDeliveryContext.Items"/>, or computes and stores it using a factory function if missing.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="context">The delivery context.</param>
    /// <param name="key">The dictionary key.</param>
    /// <param name="factory">The factory delegate used to produce the item value when missing.</param>
    /// <returns>The existing or newly generated item instance.</returns>
    public static T GetOrSetItem<T>(this WebhookDeliveryContext context, string key, Func<T> factory) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNullOrWhiteSpace(key);
        Preca.ThrowIfNull(factory);

        if(context.Items.TryGetValue(key, out object? raw) && raw is T existing) {
            return existing;
        }

        T created = factory();
        context.Items[key] = created;
        return created;
    }

    private static class EmptyHeadersDictionary {
        /// <summary>
        /// Truly immutable, zero-allocation empty dictionary that throws NotSupportedException on any mutation attempts.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Instance = ReadOnlyDictionary<string, string>.Empty;
    }
}