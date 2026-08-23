using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Security;
using Wiaoj.Webhooks.Tests.Unit.Fakes;

namespace Wiaoj.Webhooks.Tests.Unit.TestData;

/// <summary>
/// Central factory for building valid domain objects in tests.
/// </summary>
internal static class WebhookTestFactory {
    private static readonly FakeSecretProtector<WebhookSigningContext> _protector =
        FakeSecretProtectorFactory.Get<WebhookSigningContext>();

    // ── Endpoint Identifiers ──
    public static WebhookEndpointId CreateEndpointId() {
        return new(WebhookTestConstants.EndpointIdValue);
    }

    public static WebhookEndpointId CreateEndpointId(string value) {
        return new(value);
    }

    // ── Partition Keys ──
    public static WebhookPartitionKey CreatePartitionKey() {
        return new(WebhookTestConstants.EndpointIdValue);
    }

    public static WebhookPartitionKey CreatePartitionKey(string value) {
        return new(value);
    }

    // ── Target URLs ──
    public static Uri CreateTargetUrl() {
        return new(WebhookTestConstants.TargetUrlValue);
    }

    public static Uri CreateTargetUrl(string value) {
        return new(value);
    }

    // ── Secrets ──
    public static EncryptedSecret<WebhookSigningContext> CreateEncryptedSecret() {
        return _protector.Protect(WebhookTestConstants.SecretValue);
    }

    public static EncryptedSecret<WebhookSigningContext> CreateEncryptedSecret(string raw) {
        return _protector.Protect(raw);
    }

    // ── Endpoints ──
    public static WebhookEndpoint CreateEndpoint() {
        return new(CreateEndpointId(), CreateTargetUrl(), CreateEncryptedSecret());
    }

    public static WebhookEndpoint CreateEndpoint(WebhookEndpointId id) {
        return new(id, CreateTargetUrl(), CreateEncryptedSecret());
    }

    public static WebhookEndpoint CreateEndpoint(Uri targetUrl) {
        return new(CreateEndpointId(), targetUrl, CreateEncryptedSecret());
    }

    public static WebhookEndpoint CreateEndpoint(EncryptedSecret<WebhookSigningContext> secret) {
        return new(CreateEndpointId(), CreateTargetUrl(), secret);
    }

    public static WebhookEndpoint CreateEndpoint(WebhookEndpointId id, Uri targetUrl, EncryptedSecret<WebhookSigningContext> secret) {
        return new(id, targetUrl, secret);
    }

    // ── Events ──
    public static OrderCreatedWebhookEvent CreateEvent(string orderId = "ORD-1", decimal amount = 42.50m) {
        return new(orderId, amount);
    }

    // ── Jobs ──
    public static WebhookDeliveryJob CreateJob() {
        return new(WebhookJobId.NewJobId(), CreateEndpointId(), CreatePartitionKey(), WebhookTestConstants.EventTypeValue, CreateEvent());
    }

    public static WebhookDeliveryJob CreateJob(WebhookEndpointId endpointId) {
        return new(WebhookJobId.NewJobId(), endpointId, WebhookPartitionKey.From(endpointId), WebhookTestConstants.EventTypeValue, CreateEvent());
    }

    public static WebhookDeliveryJob CreateJob(WebhookEndpointId endpointId, IWebhookEvent payload) {
        return new(WebhookJobId.NewJobId(), endpointId, WebhookPartitionKey.From(endpointId), WebhookTestConstants.EventTypeValue, payload);
    }

    public static WebhookDeliveryJob CreateJob(WebhookJobId jobId, WebhookEndpointId endpointId) {
        return new(jobId, endpointId, WebhookPartitionKey.From(endpointId), WebhookTestConstants.EventTypeValue, CreateEvent());
    }

    public static WebhookDeliveryJob CreateJob(WebhookJobId jobId, WebhookEndpointId endpointId, IWebhookEvent payload) {
        return new(jobId, endpointId, WebhookPartitionKey.From(endpointId), WebhookTestConstants.EventTypeValue, payload);
    }

    public static WebhookDeliveryJob CreateJob(WebhookJobId jobId, WebhookEndpointId endpointId, string eventType, IWebhookEvent payload) {
        return new(jobId, endpointId, WebhookPartitionKey.From(endpointId), eventType, payload);
    }

    public static WebhookDeliveryJob CreateJob(WebhookJobId jobId, WebhookEndpointId endpointId, WebhookPartitionKey partitionKey, string eventType, IWebhookEvent payload) {
        return new(jobId, endpointId, partitionKey, eventType, payload);
    }

    // ── Delivery Contexts ──
    public static WebhookDeliveryContext CreateContext(
        WebhookJobId? jobId = null,
        WebhookEndpoint? endpoint = null,
        WebhookPartitionKey? partitionKey = null,
        string? eventType = null,
        IWebhookEvent? @event = null,
        string? serializedPayload = null,
        IReadOnlyList<WebhookDeliveryAttempt>? attemptHistory = null) {
        WebhookEndpoint ep = endpoint ?? CreateEndpoint();
        return new() {
            JobId = jobId ?? WebhookJobId.NewJobId(),
            Endpoint = ep,
            PartitionKey = partitionKey ?? WebhookPartitionKey.From(ep.Id),
            EventType = eventType ?? "order.created",
            Event = @event ?? CreateEvent(),
            SerializedPayload = serializedPayload ?? WebhookTestConstants.PayloadJson,
            AttemptHistory = attemptHistory ?? []
        };
    }

    public static WebhookDeliveryContext CreateContext() {
        return CreateContext(null, null, null, null, null, null, null);
    }

    public static WebhookDeliveryContext CreateContext(WebhookEndpoint endpoint) {
        return CreateContext(null, endpoint, null, null, null, null, null);
    }

    public static WebhookDeliveryContext CreateContext(IReadOnlyList<WebhookDeliveryAttempt> attemptHistory) {
        return CreateContext(null, null, null, null, null, null, attemptHistory);
    }

    // ── Deliverers ──
    public static HttpWebhookDeliverer CreateDeliverer(
        HttpMessageHandler? handler = null,
        WebhookSecurityOptions? securityOptions = null,
        TimeProvider? timeProvider = null,
        ILogger<HttpWebhookDeliverer>? logger = null) {
        handler ??= new FakeHttpMessageHandler(System.Net.HttpStatusCode.OK);
        HttpWebhookSender sender = new(new HttpClient(handler), NullLogger<HttpWebhookSender>.Instance);

        return new HttpWebhookDeliverer(
            sender,
            Microsoft.Extensions.Options.Options.Create(securityOptions ?? new WebhookSecurityOptions()),
            timeProvider ?? TimeProvider.System,
            logger ?? NullLogger<HttpWebhookDeliverer>.Instance);
    }

    // ── Delivery Results ──
    public static WebhookDeliveryResult CreateSuccessResult() {
        return WebhookDeliveryResult.Success(200, "{}");
    }

    public static WebhookDeliveryResult CreateSuccessResult(int statusCode) {
        return WebhookDeliveryResult.Success(statusCode, "{}");
    }

    public static WebhookDeliveryResult CreateSuccessResult(int statusCode, string body) {
        return WebhookDeliveryResult.Success(statusCode, body);
    }

    public static WebhookDeliveryResult CreateDuplicateResult(string key = "test_key") {
        return WebhookDeliveryResult.Duplicate(key);
    }

    public static WebhookDeliveryResult CreateFailureResult() {
        return WebhookDeliveryResult.Transient("boom");
    }

    public static WebhookDeliveryResult CreateFailureResult(string errorMessage) {
        return WebhookDeliveryResult.Transient(errorMessage);
    }

    public static WebhookDeliveryResult CreateFailureResult(string errorMessage, int statusCode) {
        return WebhookDeliveryResult.Transient(errorMessage, statusCode);
    }

    public static WebhookDeliveryResult CreateTransientFailureResult(string errorMessage = "Transient error", int statusCode = 503) {
        return WebhookDeliveryResult.Transient(errorMessage, statusCode);
    }

    public static WebhookDeliveryResult CreatePermanentFailureResult(string errorMessage = "Permanent error", int statusCode = 400) {
        return WebhookDeliveryResult.Permanent(errorMessage, statusCode, PermanentFailureReason.General);
    }

    // ── Delivery Attempts ──
    public static WebhookDeliveryAttempt CreateAttempt() {
        return new(CreateEndpointId(), 1, UnixTimestamp.Now, TimeSpan.FromMilliseconds(120), CreateSuccessResult());
    }

    public static WebhookDeliveryAttempt CreateAttempt(int attemptNumber) {
        return new(CreateEndpointId(), attemptNumber, UnixTimestamp.Now, TimeSpan.FromMilliseconds(120), CreateSuccessResult());
    }

    public static WebhookDeliveryAttempt CreateAttempt(WebhookEndpointId endpointId) {
        return new(endpointId, 1, UnixTimestamp.Now, TimeSpan.FromMilliseconds(120), CreateSuccessResult());
    }

    public static WebhookDeliveryAttempt CreateAttempt(TimeSpan duration) {
        return new(CreateEndpointId(), 1, UnixTimestamp.Now, duration, CreateSuccessResult());
    }

    public static WebhookDeliveryAttempt CreateAttempt(UnixTimestamp attemptedAt) {
        return new(CreateEndpointId(), 1, attemptedAt, TimeSpan.FromMilliseconds(120), CreateSuccessResult());
    }

    public static WebhookDeliveryAttempt CreateAttempt(WebhookDeliveryResult result) {
        return new(CreateEndpointId(), 1, UnixTimestamp.Now, TimeSpan.FromMilliseconds(120), result);
    }

    public static WebhookDeliveryAttempt CreateAttempt(int attemptNumber, WebhookDeliveryResult result) {
        return new(CreateEndpointId(), attemptNumber, UnixTimestamp.Now, TimeSpan.FromMilliseconds(120), result);
    }
}