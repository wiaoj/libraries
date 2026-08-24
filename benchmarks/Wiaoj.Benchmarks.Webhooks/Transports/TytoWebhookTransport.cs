using Microsoft.Extensions.DependencyInjection;
using Tyto;
using Wiaoj.Preconditions;
using Wiaoj.Webhooks;

namespace Wiaoj.Benchmarks.Webhooks.Transports;

/// <summary>
/// Tyto event envelope holding the webhook delivery job.
/// </summary>
[Message("webhook.delivery.job", 1)]
public sealed record TytoWebhookJobEnvelope(WebhookDeliveryJob Job) : IEvent;

/// <summary>
/// Tyto consumer dispatching the received job envelope to Wiaoj job handler.
/// </summary>
public sealed class TytoWebhookJobHandler : IEventHandler<TytoWebhookJobEnvelope> {
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TytoWebhookJobHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider instance.</param>
    public TytoWebhookJobHandler(IServiceProvider serviceProvider) {
        Preca.ThrowIfNull(serviceProvider);
        this._serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async ValueTask HandleAsync(IMessageContext<TytoWebhookJobEnvelope> context, CancellationToken cancellationToken = default) {
        using IServiceScope scope = this._serviceProvider.CreateScope();
        IWebhookJobHandler handler = scope.ServiceProvider.GetRequiredService<IWebhookJobHandler>();
        await handler.HandleAsync(context.Message.Job, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Webhook transport implementation backed by Tyto message bus.
/// </summary>
public sealed class TytoWebhookTransport : IWebhookTransport {
    private readonly IBus _bus;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TytoWebhookTransport"/> class using system clock.
    /// </summary>
    /// <param name="bus">The Tyto bus instance.</param>
    public TytoWebhookTransport(IBus bus) : this(bus, TimeProvider.System) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TytoWebhookTransport"/> class with a custom time provider.
    /// </summary>
    /// <param name="bus">The Tyto bus instance.</param>
    /// <param name="timeProvider">The time provider for delayed scheduling.</param>
    public TytoWebhookTransport(IBus bus, TimeProvider timeProvider) {
        Preca.ThrowIfNull(bus);
        Preca.ThrowIfNull(timeProvider);
        this._bus = bus;
        this._timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(job);
        return this._bus.PublishAsync(new TytoWebhookJobEnvelope(job), cancellationToken).AsTask();
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job) {
        return EnqueueAsync(job, null, CancellationToken.None);
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) {
        return EnqueueAsync(job, delay, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(job);

        if(delay.HasValue && delay.Value > TimeSpan.Zero) {
            // Non-blocking asynchronous delay before pushing into Tyto bus
            _ = Task.Delay(delay.Value, this._timeProvider, cancellationToken)
                    .ContinueWith(
                        _ => this._bus.PublishAsync(new TytoWebhookJobEnvelope(job), cancellationToken),
                        cancellationToken,
                        TaskContinuationOptions.OnlyOnRanToCompletion,
                        TaskScheduler.Default);
            return;
        }

        await this._bus.PublishAsync(new TytoWebhookJobEnvelope(job), cancellationToken).ConfigureAwait(false);
    }
}