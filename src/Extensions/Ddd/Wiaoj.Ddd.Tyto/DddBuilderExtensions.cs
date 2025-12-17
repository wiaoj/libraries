using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tyto;
using Wiaoj.Ddd;
using Wiaoj.Ddd.Abstractions;
using Wiaoj.Ddd.Tyto;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 

public static class DddBuilderExtensions {
    /// <summary>
    /// Verilen assembly'leri tarar, IIntegrationEventMapper implementasyonlarını bulur
    /// ve bunları Tyto üzerinden otomatik publish edecek şekilde ayarlar.
    /// </summary>
    public static IDddBuilder AddTytoIntegration(this IDddBuilder builder, params Assembly[] assemblies) {

        // 1. IIntegrationEventMapper<,> generic tip tanımını al
        Type openMapperType = typeof(IIntegrationEventMapper<,>);

        // 2. Her assembly'yi gez
        foreach (Assembly assembly in assemblies) {

            // 3. Class olup abstract olmayan tipleri seç
            IEnumerable<Type> types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract);

            foreach (Type type in types) {

                // 4. Bu tipin implemente ettiği interface'leri bul
                // Örn: UserRegisteredMapper : IIntegrationEventMapper<UserRegisteredDomainEvent, UserRegisteredIntegrationEvent>
                IEnumerable<Type> interfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openMapperType);

                foreach (Type? interfaceType in interfaces) {
                    // Generic argümanları çek
                    Type[] arguments = interfaceType.GetGenericArguments();
                    Type domainEventType = arguments[0];      // UserRegisteredDomainEvent
                    Type integrationEventType = arguments[1]; // UserRegisteredIntegrationEvent

                    // 5. KRİTİK KONTROL: Hedef tip Corvus'un "IEvent" arayüzünü implemente ediyor mu?
                    // Eğer etmiyorsa bu mapper Corvus için değildir, atla.
                    if (!typeof(IEvent).IsAssignableFrom(integrationEventType)) {
                        continue;
                    }

                    // 6. Mapper'ın kendisini DI'ya ekle (Scoped)
                    builder.Services.TryAddScoped(interfaceType, type);

                    // 7. AutoPublishIntegrationEventHandler'ı oluştur
                    // Hedef tip: IPostDomainEventHandler<UserRegisteredDomainEvent>
                    // Implementasyon: AutoPublishIntegrationEventHandler<UserRegisteredDomainEvent, UserRegisteredIntegrationEvent>

                    Type serviceType = typeof(IPostDomainEventHandler<>).MakeGenericType(domainEventType);
                    Type implementationType = typeof(AutoPublishIntegrationEventHandler<,>).MakeGenericType(domainEventType, integrationEventType);

                    // 8. Handler'ı DI'ya ekle
                    // TryAddEnumerable kullanıyoruz ki aynı event için başka handlerlar varsa ezmesin.
                    builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(serviceType, implementationType));
                }
            }
        }

        return builder;
    }
}
