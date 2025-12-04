using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Ddd;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Serialization.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure
public static class DependencyInjection {
    /// <param name="builder">The DDD builder.</param>
    extension(IDddBuilder builder) {
        /// <summary>
        /// Registers EF Core interceptors for auditing and domain event processing (via Outbox pattern).
        /// </summary>
        /// <typeparam name="TContext">The DbContext type.</typeparam>
        /// <param name="configureOutbox">An optional action to configure and enable the Outbox pattern.</param>
        /// <param name="configureSerializer"></param>
        /// <returns>The DDD builder for chaining.</returns>
        public IDddBuilder AddEntityFrameworkCore<TContext>(
            Action<OutboxOptions>? configureOutbox = null,
            Action<JsonSerializerOptions>? configureSerializer = null) where TContext : DbContext {
            builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);
            builder.Services.TryAddScoped<AuditInterceptor>();
            builder.Services.TryAddScoped<DomainEventDispatcherInterceptor>();

            builder.Services.AddWiaojSerializer(builder => {
                if (configureSerializer is not null) {
                    builder.UseSystemTextJson<DddEfCoreOutboxSerializerKey>(configureSerializer);
                }
                else {
                    builder.UseSystemTextJson<DddEfCoreOutboxSerializerKey>();
                }
            });

            builder.Services.Configure<OutboxOptions>(options => {
                configureOutbox?.Invoke(options);
            });

            builder.Services.AddHostedService<OutboxProcessor<TContext>>();
             
            return builder;
        }
    }

    /// <param name="optionsBuilder">The DbContext options builder.</param>
    extension(DbContextOptionsBuilder optionsBuilder) {
        /// <summary>
        /// Registers all necessary DDD interceptors (like AuditInterceptor and DomainEventDispatcherInterceptor)
        /// from the service provider. This is the recommended way to add DDD capabilities to your DbContext.
        /// This method is safe to use with Scoped interceptors when used with AddDbContext.
        /// </summary>
        /// <param name="serviceProvider">The scoped service provider, passed from the AddDbContext delegate.</param>
        /// <returns>The options builder for chaining.</returns>
        public DbContextOptionsBuilder UseDddInterceptors(IServiceProvider serviceProvider) {
            return optionsBuilder.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>(),
                serviceProvider.GetRequiredService<DomainEventDispatcherInterceptor>());
        }
    }
}