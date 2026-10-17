using Microsoft.EntityFrameworkCore;
using Wiaoj.Webhooks.EntityFrameworkCore.Internal;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks.Publishing;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Registers the EF Core subscription and publish batch stores on <see cref="IWebhookPublishingBuilder"/>.
/// </summary>
public static class WebhookPublishingBuilderEntityFrameworkCoreExtensions {
    /// <summary>
    /// Persists publishing subscriptions and publish batches in <typeparamref name="TDbContext"/>, replacing the
    /// in-memory <see cref="IWebhookSubscriptionStore"/> and the default <see cref="IWebhookBatchStore"/>.
    /// </summary>
    /// <remarks>
    /// Like <see cref="WebhookPublishingBuilderExtensions.UseBatchStore{TStore}"/>, this also starts the stale batch
    /// recovery worker, which resumes batches interrupted by a crash. <typeparamref name="TDbContext"/> must be
    /// registered in the container and call <see cref="WebhookModelBuilderExtensions.ApplyWebhooks"/> in its
    /// <c>OnModelCreating</c>.
    /// </remarks>
    /// <typeparam name="TDbContext">The application's context that maps the webhook tables.</typeparam>
    /// <param name="builder">The publishing builder being configured.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookPublishingBuilder UseEntityFrameworkCoreStore<TDbContext>(this IWebhookPublishingBuilder builder) where TDbContext : DbContext {
        Preca.ThrowIfNull(builder);
        return builder
            .UseStore<EfCoreWebhookSubscriptionStore<TDbContext>>()
            .UseBatchStore<EfCoreWebhookBatchStore<TDbContext>>();
    }
}
