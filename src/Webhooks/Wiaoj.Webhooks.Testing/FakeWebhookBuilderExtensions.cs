using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Preconditions;
using Wiaoj.Webhooks.Testing;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring fake test doubles on <see cref="IWebhookBuilder"/>.
/// </summary>
public static class FakeWebhookBuilderExtensions {

    /// <summary>
    /// Replaces real transports, dispatchers, and deliverers in DI with isolated test doubles.
    /// Ideal for integration tests, WebApplicationFactory, and local mock testing.
    /// </summary>
    /// <param name="builder">The webhook builder instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseFakeInfrastructure(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);

        WebhookTestContext context = new();
        context.RegisterServices(builder.Services);

        return builder;
    }

    /// <summary>
    /// Replaces real infrastructure in DI with isolated test doubles and exports the <see cref="WebhookTestContext"/>.
    /// </summary>
    /// <param name="builder">The webhook builder instance.</param>
    /// <param name="context">When this method returns, contains the configured test context harness.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseFakeInfrastructure(
        this IWebhookBuilder builder,
        out WebhookTestContext context) {

        Preca.ThrowIfNull(builder);

        context = new WebhookTestContext();
        context.RegisterServices(builder.Services);

        return builder;
    }

    /// <summary>
    /// Replaces the registered <see cref="IWebhookDispatcher"/> with an isolated <see cref="FakeWebhookDispatcher"/>.
    /// </summary>
    /// <param name="builder">The webhook builder instance.</param>
    /// <param name="dispatcher">When this method returns, contains the fake dispatcher instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseFakeDispatcher(
        this IWebhookBuilder builder,
        out FakeWebhookDispatcher dispatcher) {

        Preca.ThrowIfNull(builder);

        dispatcher = new FakeWebhookDispatcher();
        builder.Services.RemoveAll<IWebhookDispatcher>();
        builder.Services.AddSingleton<IWebhookDispatcher>(dispatcher);

        return builder;
    }

    /// <summary>
    /// Replaces the registered <see cref="IWebhookTransport"/> with an isolated <see cref="FakeWebhookTransport"/>.
    /// </summary>
    /// <param name="builder">The webhook builder instance.</param>
    /// <param name="transport">When this method returns, contains the fake transport instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseFakeTransport(
        this IWebhookBuilder builder,
        out FakeWebhookTransport transport) {

        Preca.ThrowIfNull(builder);

        transport = new FakeWebhookTransport();
        builder.Services.RemoveAll<IWebhookTransport>();
        builder.Services.AddSingleton<IWebhookTransport>(transport);

        return builder;
    }

    /// <summary>
    /// Replaces the registered <see cref="IWebhookDeliverer"/> with an isolated <see cref="FakeWebhookDeliverer"/>.
    /// </summary>
    /// <param name="builder">The webhook builder instance.</param>
    /// <param name="deliverer">When this method returns, contains the fake deliverer instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseFakeDeliverer(
        this IWebhookBuilder builder,
        out FakeWebhookDeliverer deliverer) {

        Preca.ThrowIfNull(builder);

        deliverer = new FakeWebhookDeliverer();
        builder.Services.RemoveAll<IWebhookDeliverer>();
        builder.Services.AddSingleton<IWebhookDeliverer>(deliverer);

        return builder;
    }
}