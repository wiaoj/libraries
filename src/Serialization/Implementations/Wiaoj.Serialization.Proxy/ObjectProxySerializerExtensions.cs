using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Preconditions;
using Wiaoj.Serialization.Proxy;

#pragma warning disable IDE0130
namespace Wiaoj.Serialization.DependencyInjection;
#pragma warning restore IDE0130 
public static class ObjectProxySerializerExtensions {
    #region UseProxy (Keyed & Keyless)

    public static ISerializerConfigurator<KeylessRegistration> UseProxy(this IWiaojSerializationBuilder builder,
        Action<ObjectProxyRegistry>? configure = null) {
        return builder.UseProxy<KeylessRegistration>(configure);
    }

    public static ISerializerConfigurator<TKey> UseProxy<TKey>(
        this IWiaojSerializationBuilder builder,
        Action<ObjectProxyRegistry>? configure = null)
        where TKey : ISerializerKey {

        Preca.ThrowIfNull(builder);

        if(builder is not IServiceCollectionAccessor accessor) {
            throw new InvalidOperationException("Builder must implement Wiaoj.Serialization.DependencyInjection.IServiceCollectionAccessor");
        }

        // Registry'yi Singleton olarak ekle ve konfigüre et
        accessor.Services.TryAddSingleton(sp => {
            ObjectProxyRegistry registry = new();
            configure?.Invoke(registry);
            return registry;
        });

        return builder.AddSerializer(sp =>
            new ObjectProxySerializer<TKey>(sp.GetRequiredService<ObjectProxyRegistry>()));
    }

    #endregion

    #region TryUseProxy

    public static ISerializerConfigurator<KeylessRegistration> TryUseProxy(
        this IWiaojSerializationBuilder builder,
        Action<ObjectProxyRegistry>? configure = null) {
        return builder.TryUseProxy<KeylessRegistration>(configure);
    }

    public static ISerializerConfigurator<TKey> TryUseProxy<TKey>(
        this IWiaojSerializationBuilder builder,
        Action<ObjectProxyRegistry>? configure = null)
        where TKey : ISerializerKey {

        Preca.ThrowIfNull(builder); 

        if(builder is not IServiceCollectionAccessor accessor) {
            throw new InvalidOperationException("Builder must implement Wiaoj.Serialization.DependencyInjection.IServiceCollectionAccessor");
        }

        accessor.Services.TryAddSingleton(sp => {
            ObjectProxyRegistry registry = new();
            configure?.Invoke(registry);
            return registry;
        });

        return builder.TryAddSerializer(sp =>
            new ObjectProxySerializer<TKey>(sp.GetRequiredService<ObjectProxyRegistry>()));
    }

    #endregion

    #region Configurator Extensions

    public static ISerializerConfigurator<TKey> ConfigureProxy<TKey>(this ISerializerConfigurator<TKey> configurator,
                                                                     Action<ObjectProxyRegistry> configure) where TKey : ISerializerKey {

        Preca.ThrowIfNull(configurator);
        Preca.ThrowIfNull(configure);

        if(configurator.Builder is not IServiceCollectionAccessor accessor) {
            throw new InvalidOperationException("Builder must implement Wiaoj.Serialization.DependencyInjection.IServiceCollectionAccessor");
        }

        accessor.Services.RemoveAll<ObjectProxyRegistry>();
        accessor.Services.TryAddSingleton(sp => {
            ObjectProxyRegistry registry = new();
            configure?.Invoke(registry);
            return registry;
        });

        return configurator;
    }

    #endregion
}