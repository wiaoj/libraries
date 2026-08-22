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
        /// Adds DDD architecture services to the specified <see cref="IServiceCollection"/>.
        /// Initializes the <see cref="IDddBuilder"/> and default dispatcher.
        /// </summary>
        /// <param name="configure">An action to configure the DDD builder (e.g., adding handlers, repositories).</param>
        /// <returns>The <see cref="IDddBuilder"/> instance for further configuration chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
        public IDddBuilder AddDdd(Action<IDddBuilder> configure) {
            Preca.ThrowIfNull(configure);

            // Builder pattern: Ensure builder exists or create a new one.
            if(services.FirstOrDefault(x => x.ServiceType == typeof(IDddBuilder))?.ImplementationInstance is not DddBuilder builder) {
                builder = new DddBuilder(services);
                services.AddSingleton<IDddBuilder>(builder);

                // Register default InMemory dispatcher if no other dispatcher is registered.
                services.TryAddScoped<IDomainEventDispatcher, InMemoryDomainEventDispatcher>();
            }

            configure(builder);
            return builder;
        }
    }
}
