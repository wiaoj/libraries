using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.Webhooks.Testing;

/// <summary>
/// A lightweight, pre-wired test harness that automates test double creation for webhook unit and integration tests.
/// </summary>
public sealed class WebhookTestContext {
    /// <summary>Gets the time provider driving the test harness.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>Gets or sets the fake dispatcher double.</summary>
    public FakeWebhookDispatcher Dispatcher { get; set; }

    /// <summary>Gets or sets the fake publisher double.</summary>
    public FakeWebhookPublisher Publisher { get; set; }

    /// <summary>Gets or sets the fake transport double.</summary>
    public FakeWebhookTransport Transport { get; set; }

    /// <summary>Gets or sets the fake endpoint resolver double.</summary>
    public FakeWebhookEndpointResolver EndpointResolver { get; set; }

    /// <summary>Gets or sets the fake deliverer double.</summary>
    public FakeWebhookDeliverer Deliverer { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookTestContext"/> class using a fixed test clock.
    /// </summary>
    public WebhookTestContext()
        : this(new FakeTimeProvider(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero))) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookTestContext"/> class with a custom time provider.
    /// </summary>
    /// <param name="timeProvider">The time provider instance.</param>
    public WebhookTestContext(TimeProvider timeProvider) {
        Preca.ThrowIfNull(timeProvider);

        this.TimeProvider = timeProvider;
        this.Dispatcher = new FakeWebhookDispatcher();
        this.Publisher = new FakeWebhookPublisher();
        this.Transport = new FakeWebhookTransport();
        this.EndpointResolver = new FakeWebhookEndpointResolver();
        this.Deliverer = new FakeWebhookDeliverer();
    }

    /// <summary>
    /// Registers all configured test doubles into the specified service collection.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <returns>This context instance for fluent method chaining.</returns>
    public WebhookTestContext RegisterServices(IServiceCollection services) {
        Preca.ThrowIfNull(services);

        services.RemoveAll<IWebhookDispatcher>();
        services.RemoveAll<IWebhookPublisher>();
        services.RemoveAll<IWebhookTransport>();
        services.RemoveAll<IWebhookEndpointResolver>();
        services.RemoveAll<IWebhookDeliverer>();
        services.RemoveAll<TimeProvider>();

        services.AddSingleton<IWebhookDispatcher>(this.Dispatcher);
        services.AddSingleton<IWebhookPublisher>(this.Publisher);
        services.AddSingleton<IWebhookTransport>(this.Transport);
        services.AddSingleton<IWebhookEndpointResolver>(this.EndpointResolver);
        services.AddSingleton<IWebhookDeliverer>(this.Deliverer);
        services.AddSingleton<TimeProvider>(this.TimeProvider);

        return this;
    }
}