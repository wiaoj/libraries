using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.Webhooks.Testing;

/// <summary>
/// High-level test harness orchestrating in-memory webhook dispatching, transport buffering,
/// endpoint directory mocking, and deterministic time travel.
/// </summary>
public sealed class WebhookTestHarness {
    /// <summary>Gets the fake dispatcher instance.</summary>
    public FakeWebhookDispatcher Dispatcher { get; }

    /// <summary>Gets the fake publisher instance.</summary>
    public FakeWebhookPublisher Publisher { get; }

    /// <summary>Gets the fake transport instance.</summary>
    public FakeWebhookTransport Transport { get; }

    /// <summary>Gets the fake endpoint resolver instance.</summary>
    public FakeWebhookEndpointResolver EndpointResolver { get; }

    /// <summary>Gets the fake deliverer instance.</summary>
    public FakeWebhookDeliverer Deliverer { get; }

    /// <summary>Gets the deterministic time provider driving delays and timeouts.</summary>
    public FakeTimeProvider TimeProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookTestHarness"/> class.
    /// </summary>
    public WebhookTestHarness() {
        this.TimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));
        this.Dispatcher = new FakeWebhookDispatcher();
        this.Publisher = new FakeWebhookPublisher();
        this.Transport = new FakeWebhookTransport();
        this.EndpointResolver = new FakeWebhookEndpointResolver();
        this.Deliverer = new FakeWebhookDeliverer();
    }

    /// <summary>
    /// Registers all test doubles into the specified service collection.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <returns>This harness instance for fluent chaining.</returns>
    public WebhookTestHarness RegisterServices(IServiceCollection services) {
        Preca.ThrowIfNull(services);

        services.AddSingleton<IWebhookDispatcher>(this.Dispatcher);
        services.AddSingleton<IWebhookPublisher>(this.Publisher);
        services.AddSingleton<IWebhookTransport>(this.Transport);
        services.AddSingleton<IWebhookEndpointResolver>(this.EndpointResolver);
        services.AddSingleton<IWebhookDeliverer>(this.Deliverer);
        services.AddSingleton<TimeProvider>(this.TimeProvider);

        return this;
    }
}