#pragma warning disable IDE0130
using Wiaoj.Webhooks.AspNetCore.Context;

namespace Wiaoj.Webhooks.AspNetCore;
#pragma warning restore IDE0130

/// <summary>
/// Defines a class-based handler for executing application business logic upon receiving a verified webhook event.
/// </summary>
/// <typeparam name="TEvent">The type of event handled.</typeparam>
public interface IWebhookReceiverHandler<TEvent> where TEvent : class {
    /// <summary>
    /// Executes the business logic for the verified incoming webhook.
    /// </summary>
    /// <param name="context">The verified webhook receiver context.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    Task HandleAsync(WebhookReceiverContext<TEvent> context, CancellationToken cancellationToken = default);
}