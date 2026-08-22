using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Security;
using Wiaoj.Webhooks.Tests.Unit.Fakes;

namespace Wiaoj.Webhooks.Tests.Unit.TestData;

/// <summary>
/// Central factory for building valid domain objects in tests, so individual test methods
/// only override the field they actually care about instead of repeating boilerplate.
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
    public static OrderCreatedWebhookEvent CreateEvent() {
        return new();
    }

    // ── Delivery Contexts ──

    public static WebhookDeliveryContext CreateContext(WebhookJobId? jobId = null,
                                                       WebhookEndpoint? endpoint = null,
                                                       IWebhookEvent? @event = null,
                                                       string? serializedPayload = null,
                                                       IReadOnlyList<WebhookDeliveryAttempt>? attemptHistory = null) {
        return new() {
            JobId = jobId ?? WebhookJobId.NewJobId(),
            Endpoint = endpoint ?? CreateEndpoint(),
            Event = @event ?? CreateEvent(),
            SerializedPayload = serializedPayload ?? WebhookTestConstants.PayloadJson,
            AttemptHistory = attemptHistory ?? []
        };
    }

    public static WebhookDeliveryContext CreateContext() {
        return new() {
            JobId = WebhookJobId.NewJobId(),
            Endpoint = CreateEndpoint(),
            Event = CreateEvent(),
            SerializedPayload = WebhookTestConstants.PayloadJson,
            AttemptHistory = []
        };
    }

    public static WebhookDeliveryContext CreateContext(WebhookEndpoint endpoint) {
        return new() {
            JobId = WebhookJobId.NewJobId(),
            Endpoint = endpoint,
            Event = CreateEvent(),
            SerializedPayload = WebhookTestConstants.PayloadJson,
            AttemptHistory = []
        };
    }

    public static WebhookDeliveryContext CreateContext(IReadOnlyList<WebhookDeliveryAttempt> attemptHistory) {
        return new() {
            JobId = WebhookJobId.NewJobId(),
            Endpoint = CreateEndpoint(),
            Event = CreateEvent(),
            SerializedPayload = WebhookTestConstants.PayloadJson,
            AttemptHistory = attemptHistory
        };
    }

    // ── Deliverers ──

    public static HttpWebhookDeliverer CreateDeliverer(
        HttpMessageHandler? handler = null,
        WebhookSecurityOptions? securityOptions = null,
        ILogger<HttpWebhookDeliverer>? logger = null) {

        handler ??= new FakeHttpMessageHandler(System.Net.HttpStatusCode.OK);
        HttpWebhookSender sender = new(new HttpClient(handler), NullLogger<HttpWebhookSender>.Instance);

        return new HttpWebhookDeliverer(
            sender,
            Microsoft.Extensions.Options.Options.Create(securityOptions ?? new WebhookSecurityOptions()),
            logger ?? NullLogger<HttpWebhookDeliverer>.Instance);
    }

    // ── Delivery Results (Discriminated Union) ──
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

    // Backward-compatible generic failure (maps to Transient)
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

    public static WebhookDeliveryResult CreateTransientFailureResult(string errorMessage, int statusCode, TimeSpan retryAfter) {
        return WebhookDeliveryResult.Transient(errorMessage, statusCode, retryAfter);
    }

    public static WebhookDeliveryResult CreatePermanentFailureResult(string errorMessage = "Permanent error", int statusCode = 400) {
        return WebhookDeliveryResult.Permanent(errorMessage, statusCode, PermanentFailureReason.General);
    }

    public static WebhookDeliveryResult CreatePermanentFailureResult(string errorMessage, PermanentFailureReason reason) {
        return WebhookDeliveryResult.Permanent(errorMessage, reason);
    }

    public static WebhookDeliveryResult CreatePermanentFailureResult(string errorMessage, int statusCode, PermanentFailureReason reason) {
        return WebhookDeliveryResult.Permanent(errorMessage, statusCode, reason);
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

    public static WebhookDeliveryAttempt CreateAttempt(int attemptNumber, UnixTimestamp attemptedAt, WebhookDeliveryResult result) {
        return new(CreateEndpointId(), attemptNumber, attemptedAt, TimeSpan.FromMilliseconds(120), result);
    }
}