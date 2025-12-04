using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wiaoj.Extensions.DependencyInjection;

public static class DecoratorServiceCollectionExtensions {
    extension(IServiceCollection services) {
        /// <summary>
        /// Decorates a previously registered service of type TService with a decorator of type TDecorator.
        /// This method is safe to use and handles all registration types (instance, factory, type).
        /// </summary>
        /// <typeparam name="TService">The type of the service to decorate.</typeparam>
        /// <typeparam name="TDecorator">The type of the decorator.</typeparam> 
        /// <exception cref="InvalidOperationException">Thrown if TService is not registered.</exception>
        public void Decorate<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator>()
            where TService : class
            where TDecorator : class, TService {
            ServiceDescriptor? originalDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService));

            Preca.ThrowIfNull(
                originalDescriptor,
                () => new InvalidOperationException($"Cannot decorate service '{typeof(TService).Name}' because it is not registered."));

            ObjectFactory objectFactory = ActivatorUtilities.CreateFactory(typeof(TDecorator), [typeof(TService)]);

            services.Replace(ServiceDescriptor.Describe(
                serviceType: typeof(TService),
                implementationFactory: provider => {
                    object originalService = GetOriginalServiceInstance(provider, originalDescriptor);

                    return objectFactory(provider, [originalService]);
                },
                lifetime: originalDescriptor.Lifetime));
        }

        /// <summary>
        /// Decorates a previously registered service of type TService using the provided decorator factory.
        /// This method provides flexibility for complex scenarios where access to the IServiceProvider is required during the decorator's creation.
        /// </summary>
        /// <typeparam name="TService">The type of the service to decorate.</typeparam> 
        /// <param name="decoratorFactory">A factory function that takes the service to be decorated and the service provider, and returns the decorator instance.</param>
        /// <exception cref="InvalidOperationException">Thrown if TService is not registered.</exception>
        public void Decorate<TService>(Func<TService, IServiceProvider, TService> decoratorFactory)
           where TService : class {
            ServiceDescriptor? originalDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService));

            Preca.ThrowIfNull(
                originalDescriptor,
                () => new InvalidOperationException($"Cannot decorate service '{typeof(TService).Name}' because it is not registered."));

            services.Replace(ServiceDescriptor.Describe(
                serviceType: typeof(TService),
                implementationFactory: provider => {
                    TService originalService = (TService)GetOriginalServiceInstance(provider, originalDescriptor);

                    return decoratorFactory(originalService, provider);
                },
                lifetime: originalDescriptor.Lifetime));
        }
    }

    private static object GetOriginalServiceInstance(IServiceProvider provider, ServiceDescriptor descriptor) {
        if (descriptor.ImplementationInstance is not null) {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null) {
            return descriptor.ImplementationFactory(provider);
        }

        if (descriptor.ImplementationType is not null) {
            // Bu, en kritik kısım. Servisi, DI container'ın kendisinden değil,
            // doğrudan ActivatorUtilities ile oluşturuyoruz. Bu, döngüsel bağımlılık
            // tuzağına düşmemizi engeller.
            return ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException($"Could not create an instance of the original service with descriptor '{descriptor}' to be decorated.");
    }
}