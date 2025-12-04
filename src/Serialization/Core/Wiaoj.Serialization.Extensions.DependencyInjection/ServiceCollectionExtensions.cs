using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Serialization.Abstractions;

namespace Wiaoj.Serialization.Extensions.DependencyInjection;
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds Wiaoj serializer support to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configurationBuilder">A delegate to configure serializers using <see cref="WiaojSerializationBuilder"/>.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddWiaojSerializer(this IServiceCollection services, Action<IWiaojSerializationBuilder> configurationBuilder) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configurationBuilder);
        WiaojSerializationBuilder builder = new(services);
        configurationBuilder(builder);
        builder.AddSerializerProvider();
        builder.Services.AddRecyclableMemoryStreamManager();
        builder.Build();

        return services;
    }

    public static IServiceCollection AddWiaojSerializer(this IServiceCollection services) {
        return AddWiaojSerializer(services, (_) => { });
    }
}

public static class WiaojserializationBuilderExtensions {
    public static IWiaojSerializationBuilder AddSerializerProvider(this IWiaojSerializationBuilder builder) {
        builder.Services.TryAddSingleton<ISerializerProvider, SerializerProvider>();
        return builder;
    }
}

internal sealed class SerializerProvider(IServiceProvider sp) : ISerializerProvider {
    private readonly ConcurrentDictionary<Type, ISerializer> _serializerTypes = new();
    public ISerializer<TKey> GetSerializer<TKey>() where TKey : notnull, ISerializerKey {
        return sp.GetRequiredService<ISerializer<TKey>>();
    }

    public ISerializer GetSerializer() {
        return sp.GetRequiredService<ISerializer>();
    }

    public ISerializer GetSerializer([NotNull] Type keyType) {

        return _serializerTypes.GetOrAdd(keyType, type => {
            Preca.ThrowIfFalse(
                typeof(ISerializerKey).IsAssignableFrom(type),
                () => new PrecaArgumentException($"Type {type} must implement {nameof(ISerializerKey)}", nameof(type)));

            Type serializerType = typeof(ISerializer<>).MakeGenericType(type);
            return (ISerializer)sp.GetRequiredService(serializerType);
        });
    }
}