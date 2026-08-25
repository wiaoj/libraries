using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Wiaoj.Primitives.Buffers;
using Wiaoj.Serialization;
using Wiaoj.Webhooks.AspNetCore.Context;
using Wiaoj.Webhooks.AspNetCore.Diagnostics;
using Wiaoj.Webhooks.AspNetCore.Metadata;

namespace Wiaoj.Webhooks.AspNetCore.Filters;

/// <summary>
/// Inbound multiplexer endpoint filter handling DoS stream protection, signature verification,
/// discriminator extraction, and dynamic event dispatching across registered hub handlers.
/// </summary>
public sealed class WebhookHubEndpointFilter : IEndpointFilter {
    private readonly WebhookHubMetadata _metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookHubEndpointFilter"/> class.
    /// </summary>
    /// <param name="metadata">The hub endpoint metadata.</param>
    public WebhookHubEndpointFilter(WebhookHubMetadata metadata) {
        Preca.ThrowIfNull(metadata);
        this._metadata = metadata;
    }

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
        HttpContext httpContext = context.HttpContext;
        IServiceProvider sp = httpContext.RequestServices;

        ILogger logger = sp.GetRequiredService<ILogger<WebhookHubEndpointFilter>>();
        ISerializer<WebhookSerializerKey> serializer = sp.GetRequiredService<ISerializer<WebhookSerializerKey>>();
        IIdempotencyStore? idempotencyStore = sp.GetService<IIdempotencyStore>();
        TimeProvider timeProvider = sp.GetService<TimeProvider>() ?? TimeProvider.System;
        IOptions<WebhookInboundOptions> inboundOptions = sp.GetRequiredService<IOptions<WebhookInboundOptions>>();

        long startTimestamp = Stopwatch.GetTimestamp();

        // 1. Resolve Effective Policy
        WebhookReceiverPolicy policy = ResolveEffectivePolicy(inboundOptions.Value);

        // 2. DoS Protection: Content-Length Check
        if(httpContext.Request.ContentLength.HasValue && httpContext.Request.ContentLength.Value > policy.MaxRequestBodyBytes) {
            return WebhookReceiverResponses.PayloadTooLarge(policy.MaxRequestBodyBytes, httpContext.Request.Path);
        }

        // 3. DoS Protection: Bounded Stream Reading
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
                    $"No secret resolver configured for inbound webhook policy '{policy.Name}'. Configure via policy or endpoint extension.");
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

        // 5. Extract Discriminator Event Name
        if(!policy.EventExtractor.TryExtractEventName(httpContext, rawPayload.Span, out string? eventName) || string.IsNullOrWhiteSpace(eventName)) {
            if(policy.IgnoreUnhandledEvents) {
                logger.LogInformation("Inbound webhook on path '{Path}' contained no extractable event discriminator. Ignored with 200 OK.", httpContext.Request.Path);
                return WebhookReceiverResponses.Ok;
            }
            return WebhookReceiverResponses.InvalidBody(httpContext.Request.Path);
        }

        logger.LogInboundWebhookReceived(eventName, httpContext.Request.Path);

        // 6. Match Event Registration
        if(!this._metadata.TryGetRegistration(eventName, out WebhookHubRegistration? registration) || registration is null) {
            if(policy.IgnoreUnhandledEvents) {
                logger.LogInformation("Inbound webhook event '{EventName}' on path '{Path}' has no registered handler. Ignored with 200 OK.", eventName, httpContext.Request.Path);
                return WebhookReceiverResponses.Ok;
            }
            return WebhookReceiverResponses.DeserializationFailed(eventName, httpContext.Request.Path);
        }

        // 7. Inbound Idempotency Check
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

        // 8. Payload Subtree Unwrapping & Deserialization
        ReadOnlySpan<byte> targetPayloadSlice = rawPayload.Span;
        if(policy.PayloadPathSegmentsUtf8 is { Length: > 0 } pathSegments) {
            if(!Utf8JsonPayloadNavigator.TryExtractSubtree(rawPayload.Span, pathSegments, out targetPayloadSlice)) {
                if(policy.EnforceIdempotency && idempotencyStore is not null && idempotencyKey.HasValue) {
                    await idempotencyStore.RemoveAsync(idempotencyKey.Value, CancellationToken.None).ConfigureAwait(false);
                }
                return WebhookReceiverResponses.DeserializationFailed(eventName, httpContext.Request.Path);
            }
        }

        object? payload = serializer.DeserializeFromString(Encoding.UTF8.GetString(targetPayloadSlice), registration.EventType);
        if(payload is null) {
            if(policy.EnforceIdempotency && idempotencyStore is not null && idempotencyKey.HasValue) {
                await idempotencyStore.RemoveAsync(idempotencyKey.Value, CancellationToken.None).ConfigureAwait(false);
            }
            return WebhookReceiverResponses.DeserializationFailed(eventName, httpContext.Request.Path);
        }

        // 9. Handler Execution (Minimal API Delegate or DI Class Handler)
        try {
            if(registration.DelegateHandler is not null) {
                object? result = await InvokeDelegateAsync(registration.DelegateHandler, registration.EventType, payload, rawPayload, parsedSignature, idempotencyKey, eventName, httpContext, sp).ConfigureAwait(false);
                if(result is IResult httpResult) {
                    return httpResult;
                }
            }
            else {
                Type handlerServiceType = typeof(IWebhookReceiverHandler<>).MakeGenericType(registration.EventType);
                object? handlerInstance = registration.HandlerType is not null
                    ? ActivatorUtilities.GetServiceOrCreateInstance(sp, registration.HandlerType)
                    : sp.GetService(handlerServiceType);

                if(handlerInstance is null) {
                    throw new InvalidOperationException(
                        $"No handler registered for event '{eventName}' (type: '{registration.EventType.FullName}'). Register '{handlerServiceType.FullName}' in DI or provide a delegate.");
                }

                MethodInfo handleMethod = handlerServiceType.GetMethod("HandleAsync")
                    ?? throw new InvalidOperationException($"Method 'HandleAsync' not found on '{handlerServiceType.FullName}'.");

                object receiverContext = CreateReceiverContext(registration.EventType, httpContext, payload, eventName, idempotencyKey, parsedSignature, rawPayload);
                Task handleTask = (Task)handleMethod.Invoke(handlerInstance, [receiverContext, httpContext.RequestAborted])!;
                await handleTask.ConfigureAwait(false);
            }
        }
        catch {
            if(policy.EnforceIdempotency && idempotencyStore is not null && idempotencyKey.HasValue) {
                await idempotencyStore.RemoveAsync(idempotencyKey.Value, CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }

        // 10. Commit Idempotency Key
        if(policy.EnforceIdempotency && idempotencyStore is not null && idempotencyKey.HasValue) {
            await idempotencyStore.MarkProcessedAsync(idempotencyKey.Value, policy.IdempotencyWindow, httpContext.RequestAborted).ConfigureAwait(false);
        }

        double durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        logger.LogInboundWebhookProcessed(eventName, durationMs);

        return WebhookReceiverResponses.Ok;
    }

    private WebhookReceiverPolicy ResolveEffectivePolicy(WebhookInboundOptions options) {
        WebhookReceiverPolicy basePolicy;
        if(!string.IsNullOrWhiteSpace(this._metadata.PolicyName) && options.Policies.TryGetValue(this._metadata.PolicyName, out WebhookReceiverPolicy? named)) {
            basePolicy = named;
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
            EventExtractor = this._metadata.EventExtractor ?? basePolicy.EventExtractor,
            IgnoreUnhandledEvents = this._metadata.IgnoreUnhandledEvents ?? basePolicy.IgnoreUnhandledEvents,
            IdempotencyKeyExtractor = basePolicy.IdempotencyKeyExtractor,
            PayloadPath = this._metadata.PayloadPath ?? basePolicy.PayloadPath
        };
    }

    private static object CreateReceiverContext(
        Type eventType,
        HttpContext httpContext,
        object payload,
        string eventName,
        IdempotencyKey? idempotencyKey,
        WebhookSignature? signature,
        ReadOnlyMemory<byte> rawBody) {

        Type contextType = typeof(WebhookReceiverContext<>).MakeGenericType(eventType);
        object context = Activator.CreateInstance(contextType)!;

        contextType.GetProperty("HttpContext")!.SetValue(context, httpContext);
        contextType.GetProperty("Payload")!.SetValue(context, payload);
        contextType.GetProperty("EventType")!.SetValue(context, eventName);
        contextType.GetProperty("IdempotencyKey")!.SetValue(context, idempotencyKey);
        contextType.GetProperty("Signature")!.SetValue(context, signature);
        contextType.GetProperty("RawBody")!.SetValue(context, rawBody);
        contextType.GetProperty("Headers")!.SetValue(context, httpContext.Request.Headers);

        return context;
    }

    private static async Task<object?> InvokeDelegateAsync(
        Delegate handler,
        Type eventType,
        object payload,
        ReadOnlyMemory<byte> rawPayload,
        WebhookSignature? signature,
        IdempotencyKey? idempotencyKey,
        string eventName,
        HttpContext httpContext,
        IServiceProvider serviceProvider) {

        ParameterInfo[] parameters = handler.Method.GetParameters();
        object?[] arguments = new object?[parameters.Length];

        Type contextGenericType = typeof(WebhookReceiverContext<>).MakeGenericType(eventType);

        for(int i = 0; i < parameters.Length; i++) {
            Type paramType = parameters[i].ParameterType;

            if(paramType.IsAssignableFrom(eventType)) {
                arguments[i] = payload;
            }
            else if(paramType == contextGenericType) {
                arguments[i] = CreateReceiverContext(eventType, httpContext, payload, eventName, idempotencyKey, signature, rawPayload);
            }
            else if(paramType == typeof(HttpContext)) {
                arguments[i] = httpContext;
            }
            else if(paramType == typeof(CancellationToken)) {
                arguments[i] = httpContext.RequestAborted;
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