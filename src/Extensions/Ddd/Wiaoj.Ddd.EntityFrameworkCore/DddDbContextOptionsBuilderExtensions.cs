using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// EF Core extension methods for attaching DDD interceptors to <see cref="DbContextOptionsBuilder"/>.
/// </summary>
public static class DddDbContextOptionsBuilderExtensions {
    extension(DbContextOptionsBuilder optionsBuilder) {
        /// <summary>
        /// Attaches the DDD interceptors (audit + domain event dispatcher / outbox) that were registered
        /// in DI by <c>AddEntityFrameworkCore</c>. Call this inside your <c>AddDbContext</c> or
        /// <c>AddDbContextFactory</c> delegate, passing the delegate's service provider.
        /// </summary>
        /// <remarks>
        /// EF Core does not reliably auto-discover DI-registered interceptors for every registration mode
        /// (notably <c>AddDbContextFactory</c> and pooling), so this explicit hook guarantees the
        /// interceptors run regardless of how the context is registered. Only the interceptors belonging to
        /// <typeparamref name="TContext"/> are attached (the shared audit interceptor and this context's
        /// dispatcher), so contexts that do not opt in are never touched.
        /// </remarks>
        /// <typeparam name="TContext">The concrete <see cref="DbContext"/> being configured.</typeparam>
        /// <param name="serviceProvider">The service provider supplied to the AddDbContext(Factory) delegate.</param>
        /// <returns>The options builder for chaining.</returns>
        public DbContextOptionsBuilder UseDddInterceptors<TContext>(IServiceProvider serviceProvider) where TContext : DbContext {
            return optionsBuilder.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<DomainEventDispatcherInterceptor<TContext>>());
        }
    }
}
