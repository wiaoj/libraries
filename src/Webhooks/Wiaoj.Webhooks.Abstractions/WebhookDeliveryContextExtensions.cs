using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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

    // ────────────────────────────────────────────────────────────────────────
    // 5. SIGNATURE ACCESSORS
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

    // ────────────────────────────────────────────────────────────────────────
    // 6. GENERIC SAFE ITEMS ACCESSORS
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

    private static class EmptyHeadersDictionary {
        /// <summary>
        /// Truly immutable, zero-allocation empty dictionary that throws NotSupportedException on any mutation attempts.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Instance = ReadOnlyDictionary<string, string>.Empty;
    }
}