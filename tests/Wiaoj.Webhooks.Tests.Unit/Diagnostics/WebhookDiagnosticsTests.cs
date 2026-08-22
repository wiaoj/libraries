using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Security;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory;

namespace Wiaoj.Webhooks.Tests.Unit.Diagnostics;

[Collection("DiagnosticsTests")]
public sealed class WebhookDiagnosticsTests {
    private static HttpWebhookDeliverer CreateDeliverer(
        HttpMessageHandler handler,
        WebhookSecurityOptions? securityOptions = null) {
        HttpWebhookSender sender = new(new HttpClient(handler), NullLogger<HttpWebhookSender>.Instance);
        return new HttpWebhookDeliverer(
            sender,
            Microsoft.Extensions.Options.Options.Create(securityOptions ?? new WebhookSecurityOptions()),
            NullLogger<HttpWebhookDeliverer>.Instance);
    }

    [Fact]
    public async Task PipelineRunner_StartsActivity_AndSetsActivityTags() {
        Activity? capturedActivity = null;
        using ActivityListener listener = new() {
            ShouldListenTo = source => source.Name == "Wiaoj.Webhooks",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if(activity.OperationName == "webhook.deliver") {
                    capturedActivity = activity;
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        FakeWebhookDeliverer deliverer = new(WebhookTestFactory.CreateSuccessResult(200));
        WebhookPipelineRunner runner = new([], deliverer, TimeProvider.System, NullLogger<WebhookPipelineRunner>.Instance);
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

        await runner.RunAsync(context);

        Assert.NotNull(capturedActivity);
        Assert.Equal(context.Endpoint.Id.Value, capturedActivity.GetTagItem("webhook.endpoint_id"));
        Assert.Equal(context.TargetUrl.ToString(), capturedActivity.GetTagItem("webhook.target_url"));
        Assert.Equal(1, capturedActivity.GetTagItem("webhook.attempt_number"));
        Assert.Equal(true, capturedActivity.GetTagItem("webhook.success"));
        Assert.Equal(200, capturedActivity.GetTagItem("webhook.status_code"));
    }

    [Fact]
    public async Task Dispatcher_StartsActivity_AndRecordsMetric() {
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-tracing-unique-id");
        Activity? capturedActivity = null;
        using ActivityListener listener = new() {
            ShouldListenTo = source => source.Name == "Wiaoj.Webhooks",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if(activity.OperationName == "webhook.dispatch" && (string?)activity.GetTagItem("webhook.endpoint_id") == endpointId.Value) {
                    capturedActivity = activity;
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        long measurementValue = 0;
        using MeterListener meterListener = new();
        meterListener.InstrumentPublished = (instrument, meterListenerInstance) => {
            if(instrument.Meter.Name == "Wiaoj.Webhooks" && instrument.Name == "wiaoj.webhooks.dispatch.count") {
                meterListenerInstance.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => {
            bool matchesEndpoint = false;
            foreach(KeyValuePair<string, object?> tag in tags) {
                if(tag.Key == "webhook.endpoint_id" && (string?)tag.Value == endpointId.Value) {
                    matchesEndpoint = true;
                    break;
                }
            }
            if(matchesEndpoint) {
                measurementValue += measurement;
            }
        });
        meterListener.Start();

        InMemoryWebhookTransport transport = new();
        InMemoryWebhookStore store = new();
        SystemTextJsonSerializer<WebhookSerializerKey> serializer = new();
        FakeTimeProvider timeProvider = new();
        WebhookDispatcher dispatcher = new(store, transport, serializer, timeProvider, NullLogger<WebhookDispatcher>.Instance);
        OrderCreatedWebhookEvent @event = WebhookTestFactory.CreateEvent();

        await dispatcher.DispatchAsync(endpointId, @event);

        meterListener.RecordObservableInstruments();

        Assert.NotNull(capturedActivity);
        Assert.Equal(endpointId.Value, capturedActivity.GetTagItem("webhook.endpoint_id"));
        Assert.Equal(OrderCreatedWebhookEvent.EventName, capturedActivity.GetTagItem("webhook.event_name")); ;
        Assert.Equal(1, measurementValue);
    }

    [Fact]
    public async Task HttpWebhookDeliverer_HandlesTimeout_Gracefully() {
        // Arrange
        FakeTimeoutHttpMessageHandler timeoutHandler = new();
        HttpWebhookDeliverer deliverer = CreateDeliverer(timeoutHandler);
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

        // Act
        WebhookDeliveryResult result = await deliverer.DeliverAsync(context, CancellationToken.None);

        // Assert
        WebhookDeliveryResult.TransientFailure failure = Assert.IsType<WebhookDeliveryResult.TransientFailure>(result);
        Assert.False(failure.IsSuccess);
        Assert.Contains("timed out", failure.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HttpWebhookDeliverer_HandlesHttpRequestException_Gracefully() {
        // Arrange
        FakeThrowingHttpMessageHandler errorHandler = new(
            new HttpRequestException("Connection refused", null, HttpStatusCode.ServiceUnavailable));
        HttpWebhookDeliverer deliverer = CreateDeliverer(errorHandler);
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

        // Act
        WebhookDeliveryResult result = await deliverer.DeliverAsync(context, CancellationToken.None);

        // Assert
        WebhookDeliveryResult.TransientFailure failure = Assert.IsType<WebhookDeliveryResult.TransientFailure>(result);
        Assert.False(failure.IsSuccess);
        Assert.Equal(503, failure.StatusCode);
        Assert.Contains("Connection refused", failure.ErrorMessage);
    }

    private sealed class FakeTimeoutHttpMessageHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            throw new OperationCanceledException("The request timed out (simulated).");
        }
    }

    private sealed class FakeThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            throw exception;
        }
    }
}
