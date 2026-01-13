using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wiaoj.Extensions.DependencyInjection; 
public static class DecoratorServiceCollectionExtensions {

    #region Basic Decoration (Closed & Open)

    public static IServiceCollection Decorate<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService {
        return services.Decorate(typeof(TService), typeof(TDecorator));
    }

    public static IServiceCollection TryDecorate<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService {
        return services.DecorateInternal(typeof(TService), typeof(TDecorator), throwOnNotFound: false);
    }

    public static IServiceCollection Decorate(this IServiceCollection services, Type serviceType, Type decoratorType) {
        return services.DecorateInternal(serviceType, decoratorType, throwOnNotFound: true);
    }

    #endregion

    #region Predicate Based Decoration

    public static IServiceCollection Decorate(this IServiceCollection services, Type serviceType, Type decoratorType, Func<ServiceDescriptor, bool> predicate) {
        return services.DecorateInternal(serviceType, decoratorType, throwOnNotFound: true, predicate);
    }

    #endregion

    #region Factory Based Decoration

    public static IServiceCollection Decorate<TService>(this IServiceCollection services, Func<TService, IServiceProvider, TService> decoratorFactory)
        where TService : class {

        // Liste kopyası alıyoruz ki döngüde koleksiyon değişti hatası almayalım
        var descriptors = services.Where(d => d.ServiceType == typeof(TService)).ToList();

        if(descriptors.Count == 0)
            throw new InvalidOperationException($"Service '{typeof(TService).Name}' not found.");

        foreach(var descriptor in descriptors) {
            var index = services.IndexOf(descriptor);
            // Factory kullanırken orijinal instance'ı manuel oluşturmalıyız
            services[index] = DecorateDescriptor(descriptor, (provider, key) => {
                var originalInstance = provider.CreateInstanceFromDescriptor(descriptor);
                return decoratorFactory((TService)originalInstance, provider);
            });
        }

        return services;
    }

    #endregion

    #region Internal Core Logic

    private static IServiceCollection DecorateInternal(
        this IServiceCollection services,
        Type serviceType,
        Type decoratorType,
        bool throwOnNotFound,
        Func<ServiceDescriptor, bool>? predicate = null) {

        var descriptors = services.Where(d => d.ServiceType == serviceType && (predicate?.Invoke(d) ?? true)).ToList();

        if(descriptors.Count == 0) {
            if(throwOnNotFound)
                throw new InvalidOperationException($"No registered services found for '{serviceType.Name}'.");
            return services;
        }

        foreach(var descriptor in descriptors) {
            var index = services.IndexOf(descriptor);

            if(serviceType.IsGenericTypeDefinition) {
                // NOT: Open Generic dekorasyonu (örn: IRepository<> -> CachedRepository<>) 
                // .NET DI container'da basitçe yapılamaz çünkü araya giren decorator'ın
                // generic argümanları çalışma zamanında çözümlemesi gerekir.
                // Mevcut kodunuz burada sadece "Replace" yapıyordu.
                // Doğru davranış için burada karmaşık bir Factory yazmak gerekir.
                // Basitlik adına burada orijinal kodu koruyorum ancak UYARI:
                // Bu kod Decorator değil Replacement yapar. Zincirleme çalışmaz.

                services[index] = ServiceDescriptor.Describe(
                    serviceType,
                    decoratorType,
                    descriptor.Lifetime);
            }
            else {
                // Closed Generic veya Normal Type
                services[index] = DecorateDescriptor(descriptor, (provider, key) => {
                    // 1. Orijinal instance'ı (inner) oluştur.
                    var innerInstance = provider.CreateInstanceFromDescriptor(descriptor);

                    // 2. Decorator'ı oluştur ve inner instance'ı parametre olarak geçmeye çalış.
                    // ActivatorUtilities, constructor'da eşleşen tip varsa innerInstance'ı kullanır.
                    return ActivatorUtilities.CreateInstance(provider, decoratorType, innerInstance);
                });
            }
        }

        return services;
    }

    /// <summary>
    /// Mevcut descriptor'ı bir factory ile sarmalayarak yeni bir descriptor oluşturur.
    /// </summary>
    private static ServiceDescriptor DecorateDescriptor(ServiceDescriptor descriptor, Func<IServiceProvider, object?, object> factory) {
        // .NET 8 Keyed Service desteği
        if(descriptor.IsKeyedService) {
            return new ServiceDescriptor(
                descriptor.ServiceType,
                descriptor.ServiceKey,
                (sp, key) => factory(sp, key), // Key bilgisini taşıyoruz
                descriptor.Lifetime);
        }

        return new ServiceDescriptor(
            descriptor.ServiceType,
            sp => factory(sp, null),
            descriptor.Lifetime);
    }

    /// <summary>
    /// Servis konteynerini (Provider) kullanmadan, Descriptor üzerindeki bilgilere göre
    /// nesneyi manuel oluşturur. Bu sonsuz döngüyü engeller.
    /// </summary>
    private static object CreateInstanceFromDescriptor(this IServiceProvider provider, ServiceDescriptor descriptor) {
        // 1. Hazır instance varsa döndür
        if(descriptor.ImplementationInstance != null)
            return descriptor.ImplementationInstance;

        // 2. Keyed Factory varsa çalıştır
        if(descriptor.IsKeyedService && descriptor.KeyedImplementationFactory != null)
            return descriptor.KeyedImplementationFactory(provider, descriptor.ServiceKey);

        // 3. Normal Factory varsa çalıştır
        if(descriptor.ImplementationFactory != null)
            return descriptor.ImplementationFactory(provider);

        // 4. ImplementationType varsa ActivatorUtilities ile oluştur
        if(descriptor.ImplementationType != null) {
            // Keyed service olsa bile ImplementationType belliyse buradan oluşturulabilir.
            return ActivatorUtilities.GetServiceOrCreateInstance(provider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException($"Could not create instance for {descriptor.ServiceType.Name}. Descriptor is missing ImplementationType, Instance, and Factory.");
    }

    #endregion
}