using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Ddd;
using Wiaoj.Ddd.DomainEvents;
using Wiaoj.Ddd.Internal;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Service collection extension methods for registering DDD architecture services.
/// </summary>
public static class DddServiceCollectionExtensions {
    extension(IServiceCollection services) {
        /// <summary>
        /// Adds DDD architecture services to the specified <see cref="IServiceCollection"/> and returns the
        /// <see cref="IDddBuilder"/> to register handlers and integrations on.
        /// </summary>
        /// <returns>The DDD builder. Calling this again returns the same builder.</returns>
        public IDddBuilder AddDdd() {
            // One builder per service collection: modules each call AddDdd and share it.
            if(services.FirstOrDefault(x => x.ServiceType == typeof(IDddBuilder))?.ImplementationInstance is DddBuilder existing) {
                return existing;
            }

            DddBuilder builder = new(services);
            services.AddSingleton<IDddBuilder>(builder);

            // Register default InMemory dispatcher if no other dispatcher is registered.
            services.TryAddScoped<IDomainEventDispatcher, InMemoryDomainEventDispatcher>();

            return builder;
        }

        /// <summary>
        /// Adds DDD architecture services to the specified <see cref="IServiceCollection"/> and configures the
        /// <see cref="IDddBuilder"/> inside <paramref name="configure"/>.
        /// </summary>
        /// <param name="configure">An action to configure the DDD builder (e.g., adding handlers, repositories).</param>
        /// <returns>The service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
        public IServiceCollection AddDdd(Action<IDddBuilder> configure) {
            Preca.ThrowIfNull(configure);

            configure(services.AddDdd());
            return services;
        }
    }
}
