using Microsoft.EntityFrameworkCore;
using Wiaoj.Webhooks.EntityFrameworkCore.Internal;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Registers the EF Core job store on <see cref="IWebhookBuilder"/>.
/// </summary>
public static class WebhookBuilderEntityFrameworkCoreExtensions {
    /// <summary>
    /// Persists webhook jobs, their delivery attempts and leases in <typeparamref name="TDbContext"/>, replacing the
    /// in-memory <see cref="IWebhookStore"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <typeparamref name="TDbContext"/> must be registered in the container (for example with <c>AddDbContext</c>) and
    /// must call <see cref="WebhookModelBuilderExtensions.ApplyWebhooks"/> in its <c>OnModelCreating</c>. The store is a
    /// singleton and resolves a new context from its own scope for every operation, so it never shares a context
    /// with application code.
    /// </para>
    /// <para>
    /// The provider must be relational: leases and status changes are issued as <c>ExecuteUpdate</c> statements.
    /// </para>
    /// </remarks>
    /// <typeparam name="TDbContext">The application's context that maps the webhook tables.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseEntityFrameworkCoreStore<TDbContext>(this IWebhookBuilder builder) where TDbContext : DbContext {
        Preca.ThrowIfNull(builder);
        return builder.UseStore<EfCoreWebhookStore<TDbContext>>();
    }
}
