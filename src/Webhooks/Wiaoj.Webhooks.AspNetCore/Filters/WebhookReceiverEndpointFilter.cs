using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Serialization;
using Wiaoj.Webhooks.AspNetCore.Context;
using Wiaoj.Webhooks.AspNetCore.Diagnostics;
using Wiaoj.Webhooks.AspNetCore.Metadata;

namespace Wiaoj.Webhooks.AspNetCore.Filters;

/// <summary>
/// High-performance endpoint filter orchestrating DoS bounded reading, cryptographic authentication,
/// idempotency deduplication, and payload dispatching.
/// </summary>
public sealed class WebhookReceiverEndpointFilter<TEvent> : IEndpointFilter where TEvent : class, IWebhookEvent {
    private readonly WebhookReceiverEndpointMetadata _metadata;
    private readonly Delegate? _delegateHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookReceiverEndpointFilter{TEvent}"/> class.
    /// </summary>
    /// <param name="metadata">The endpoint metadata containing configuration overrides.</param>
    /// <param name="delegateHandler">The optional Minimal API handler delegate.</param>
    public WebhookReceiverEndpointFilter(WebhookReceiverEndpointMetadata metadata, Delegate? delegateHandler = null) {
        Preca.ThrowIfNull(metadata);
        this._metadata = metadata;
        this._delegateHandler = delegateHandler;
    }

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        HttpContext httpContext = context.HttpContext;
        IServiceProvider sp = httpContext.RequestServices;

        ILogger logger = sp.GetRequiredService<ILogger<WebhookReceiverEndpointFilter<TEvent>>>();
        IWebhookEventRegistry eventRegistry = sp.GetRequiredService<IWebhookEventRegistry>();
        ISerializer<WebhookSerializerKey> serializer = sp.GetRequiredService<ISerializer<WebhookSerializerKey>>();
        IIdempotencyStore? idempotencyStore = sp.GetService<IIdempotencyStore>();
        TimeProvider timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
        IOptions<WebhookInboundOptions> inboundOptions = sp.GetRequiredService<IOptions<WebhookInboundOptions>>();

        string eventName = eventRegistry.GetEventName<TEvent>();
        logger.LogInboundWebhookReceived(eventName, httpContext.Request.Path);

        long startTimestamp = Stopwatch.GetTimestamp();

        // 1. Resolve Effective Policy
        WebhookReceiverPolicy policy = ResolveEffectivePolicy(inboundOptions.Value, eventName);

        // 2. DoS Protection: Content-Length Check
        if(httpContext.Request.ContentLength.HasValue && httpContext.Request.ContentLength.Value > policy.MaxRequestBodyBytes) {
            return WebhookReceiverResponses.PayloadTooLarge(policy.MaxRequestBodyBytes, httpContext.Request.Path);
        }

        // 3. DoS Protection: Bounded Stream Reading with zero intermediate string allocation
        httpContext.Request.EnableBuffering();
        await using AsyncValueBuffer<byte> buffer = new(policy.MaxRequestBodyBytes + 1);

        int totalRead = 0;
        int read;
        Stream stream = httpContext.Request.Body;
        while((read = await stream.ReadAsync(buffer.Memory[totalRead..], httpContext.RequestAborted).ConfigureAwait(false)) > 0) {
            totalRead += read;
            if(totalRead > policy.MaxRequestBodyBytes) {
                return WebhookReceiverResponses.PayloadTooLarge(policy.MaxRequestBodyBytes, httpContext.Request.Path);
            }
        }

        if(totalRead == 0) {
            return WebhookReceiverResponses.InvalidBody(httpContext.Request.Path);
        }

        httpContext.Request.Body.Position = 0;
        ReadOnlyMemory<byte> rawPayload = buffer.Memory[..totalRead];

        // 4. Cryptographic Signature Verification
        WebhookSignature? parsedSignature = null;
        if(policy.RequireSignature) {
            string signatureHeader = httpContext.Request.Headers[policy.HeaderName].ToString();
            if(string.IsNullOrWhiteSpace(signatureHeader)) {
                logger.LogInboundSignatureVerificationFailed(httpContext.Request.Path);
                return WebhookReceiverResponses.UnauthorizedSignature(httpContext.Request.Path);
            }

            if(policy.SecretResolver is null) {
                throw new InvalidOperationException(
                    $"No secret or resolver configured for inbound webhook '{eventName}'. Configure via policy or endpoint extension.");
            }

            UnixTimestamp currentTimestamp = timeProvider.GetUnixTimestamp();

            bool isValid = await policy.SecretResolver.VerifyAsync(
                httpContext,
                rawPayload,
                signatureHeader,
                policy.Signer,
                policy.Tolerance,
                currentTimestamp,
                httpContext.RequestAborted).ConfigureAwait(false);

            if(!isValid) {
                logger.LogInboundSignatureVerificationFailed(httpContext.Request.Path);
                return WebhookReceiverResponses.UnauthorizedSignature(httpContext.Request.Path);
            }

            _ = WebhookSignature.TryParse(signatureHeader, out WebhookSignature sig);
            parsedSignature = sig;
        }

        // 5. Inbound Idempotency Check (Atomic reservation to prevent TOCTOU race conditions)
        IdempotencyKey? idempotencyKey = null;
        if(policy.EnforceIdempotency && idempotencyStore is not null) {
            idempotencyKey = policy.IdempotencyKeyExtractor(httpContext, rawPayload);
            if(idempotencyKey.HasValue) {
                bool isClaimed = await idempotencyStore.TryMarkProcessedAsync(idempotencyKey.Value, policy.IdempotencyWindow, httpContext.RequestAborted).ConfigureAwait(false);
                if(!isClaimed) {
                    logger.LogInboundDuplicateSkipped(idempotencyKey.Value.Value);
                    return WebhookReceiverResponses.Ok;
                }
            }
        }

        // 6. Payload Deserialization
        ReadOnlySequence<byte> payloadSequence = new(rawPayload);
        if(!serializer.TryDeserialize(in payloadSequence, out TEvent? payload) || payload is null) {
            if(policy.EnforceIdempotency && idempotencyStore is not null && idempotencyKey.HasValue) {
                await idempotencyStore.RemoveAsync(idempotencyKey.Value, CancellationToken.None).ConfigureAwait(false);
            }
            return WebhookReceiverResponses.DeserializationFailed(eventName, httpContext.Request.Path);
        }
         
        WebhookReceiverContext<TEvent> receiverContext = new() {
            HttpContext = httpContext,
            Payload = payload,
            EventType = eventName,
            IdempotencyKey = idempotencyKey,
            Signature = parsedSignature,
            RawBody = rawPayload.ToArray(),
            Headers = httpContext.Request.Headers
        };

        // 7. Handler Execution
        try {
            if(this._delegateHandler is not null) {
                object? result = await InvokeDelegateHandlerAsync(this._delegateHandler, receiverContext, sp, httpContext.RequestAborted).ConfigureAwait(false);
                if(result is IResult httpResult) {
                    return httpResult;
                }
            }
            else {
                IWebhookReceiverHandler<TEvent>? handler = sp.GetService<IWebhookReceiverHandler<TEvent>>()
                    ?? throw new InvalidOperationException(
                        $"No handler registered for webhook event '{typeof(TEvent).FullName}'. Register '{nameof(IWebhookReceiverHandler<>)}' in DI or supply a delegate.");
                await handler.HandleAsync(receiverContext, httpContext.RequestAborted).ConfigureAwait(false);
            }
        }
        catch {
            // Rollback idempotency claim on handler failure so upstream retries can be processed
            if(policy.EnforceIdempotency && idempotencyStore is not null && idempotencyKey.HasValue) {
                await idempotencyStore.RemoveAsync(idempotencyKey.Value, CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }

        // 8. Commit Idempotency Key on Success
        if(policy.EnforceIdempotency && idempotencyStore is not null && idempotencyKey.HasValue) {
            await idempotencyStore.MarkProcessedAsync(idempotencyKey.Value, policy.IdempotencyWindow, httpContext.RequestAborted).ConfigureAwait(false);
        }

        double durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        logger.LogInboundWebhookProcessed(eventName, durationMs);

        return WebhookReceiverResponses.Ok;
    }

    private WebhookReceiverPolicy ResolveEffectivePolicy(WebhookInboundOptions options, string eventName) {
        WebhookReceiverPolicy basePolicy;
        if(!string.IsNullOrWhiteSpace(this._metadata.PolicyName) && options.Policies.TryGetValue(this._metadata.PolicyName, out WebhookReceiverPolicy? named)) {
            basePolicy = named;
        }
        else if(options.Policies.TryGetValue(eventName, out WebhookReceiverPolicy? eventPolicy)) {
            basePolicy = eventPolicy;
        }
        else {
            basePolicy = options.DefaultPolicy;
        }

        return new WebhookReceiverPolicy {
            Name = basePolicy.Name,
            HeaderName = this._metadata.HeaderName ?? basePolicy.HeaderName,
            Signer = this._metadata.Signer ?? basePolicy.Signer,
            Tolerance = this._metadata.Tolerance ?? basePolicy.Tolerance,
            MaxRequestBodyBytes = this._metadata.MaxRequestBodyBytes ?? basePolicy.MaxRequestBodyBytes,
            RequireSignature = this._metadata.RequireSignature ?? basePolicy.RequireSignature,
            EnforceIdempotency = this._metadata.EnforceIdempotency ?? basePolicy.EnforceIdempotency,
            IdempotencyWindow = this._metadata.IdempotencyWindow ?? basePolicy.IdempotencyWindow,
            SecretResolver = this._metadata.SecretResolver ?? basePolicy.SecretResolver,
            IdempotencyKeyExtractor = this._metadata.IdempotencyKeyExtractor ?? basePolicy.IdempotencyKeyExtractor
        };
    }

    private static async Task<object?> InvokeDelegateHandlerAsync(
        Delegate handler,
        WebhookReceiverContext<TEvent> receiverContext,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken) {

        ParameterInfo[] parameters = handler.Method.GetParameters();
        object?[] arguments = new object?[parameters.Length];

        for(int i = 0; i < parameters.Length; i++) {
            Type paramType = parameters[i].ParameterType;

            if(paramType == typeof(TEvent)) {
                arguments[i] = receiverContext.Payload;
            }
            else if(paramType == typeof(WebhookReceiverContext<TEvent>)) {
                arguments[i] = receiverContext;
            }
            else if(paramType == typeof(HttpContext)) {
                arguments[i] = receiverContext.HttpContext;
            }
            else if(paramType == typeof(CancellationToken)) {
                arguments[i] = cancellationToken;
            }
            else {
                arguments[i] = serviceProvider.GetService(paramType)
                    ?? throw new InvalidOperationException($"Cannot resolve service of type '{paramType.FullName}' for webhook delegate parameter '{parameters[i].Name}'.");
            }
        }

        object? result;
        try {
            result = handler.DynamicInvoke(arguments);
        }
        catch(TargetInvocationException ex) when(ex.InnerException is not null) {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        switch(result) {
            case Task<IResult> taskResult:
                return await taskResult.ConfigureAwait(false);
            case Task task:
                await task.ConfigureAwait(false);
                return task.GetType().IsGenericType ? ((dynamic)task).Result : null;
            case ValueTask<IResult> valueTaskResult:
                return await valueTaskResult.ConfigureAwait(false);
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                return null;
            default:
                return result;
        }
    }
}