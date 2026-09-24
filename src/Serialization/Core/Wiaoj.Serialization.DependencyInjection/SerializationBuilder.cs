using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wiaoj.Serialization.DependencyInjection;
/// <inheritdoc /> 
internal sealed class SerializationBuilder : ISerializationBuilder, IServiceCollectionAccessor {
    public IServiceCollection Services { get; }

    internal SerializationBuilder(IServiceCollection services) {
        this.Services = services;
    }


    /// <inheritdoc /> 
    public ISerializerConfigurator<TKey> AddSerializer<TKey>(Func<IServiceProvider, ISerializer<TKey>> factory) where TKey : ISerializerKey {
        Preca.ThrowIfNull(factory);
        Type serviceType = typeof(ISerializer<TKey>);
        if(this.Services.Any(sd => sd.ServiceType == serviceType)) {
            Preca.ThrowIfTrue(
                typeof(TKey) == typeof(KeylessRegistration),
                () => new InvalidOperationException("A keyless (default) serializer has already been registered. Only one keyless registration is allowed."));

            throw new InvalidOperationException($"A serializer with the key '{typeof(TKey).FullName}' has already been registered.");
        }

        this.Services.AddSingleton<ISerializer<TKey>>(factory);
        return new SerializerConfigurator<TKey>(this);
    }

    /// <inheritdoc /> 
    public ISerializerConfigurator<KeylessRegistration> AddSerializer(Func<IServiceProvider, ISerializer<KeylessRegistration>> factory) {
        return AddSerializer<KeylessRegistration>(factory);
    }

    /// <inheritdoc /> 
    public ISerializerConfigurator<TKey> TryAddSerializer<TKey>(Func<IServiceProvider, ISerializer<TKey>> factory) where TKey : ISerializerKey {
        Preca.ThrowIfNull(factory);
        Type serviceType = typeof(ISerializer<TKey>);

        // Kontrol et: Eğer bu Key tipiyle kayıtlı bir servis YOKSA ekle.
        if(!this.Services.Any(sd => sd.ServiceType == serviceType)) {
            this.Services.AddSingleton<ISerializer<TKey>>(factory);
        }

        // Varsa eklemiyoruz ama null dönmüyoruz. 
        // Böylece kullanıcı .TryAddSerializer(...).Builder... diyerek zinciri kırmamış olur.
        return new SerializerConfigurator<TKey>(this);
    }

    /// <inheritdoc /> 
    public ISerializerConfigurator<KeylessRegistration> TryAddSerializer(Func<IServiceProvider, ISerializer<KeylessRegistration>> factory) {
        return TryAddSerializer<KeylessRegistration>(factory);
    }

    /// <summary>
    /// Chooses the serializer resolved as the non-keyed <see cref="ISerializer"/>: the keyless registration when there is
    /// one, otherwise the only registered serializer; with several and no keyless one there is no default.
    /// </summary>
    /// <param name="provider">The service provider resolving the default.</param>
    /// <param name="services">The service collection the serializers were registered in.</param>
    /// <returns>The default serializer, or <see langword="null"/> when there is none.</returns>
    /// <remarks>
    /// Runs when <see cref="ISerializer"/> is first resolved rather than when <c>AddWiaojSerializer</c> returns, so
    /// serializers registered after it — fluent calls on the returned builder — are taken into account.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The only registered serializer's key does not implement <see cref="ISerializerKey"/>.</exception>
    internal static ISerializer? ResolveDefault(IServiceProvider provider, IServiceCollection services) {
        if(services.Any(static sd => sd.ServiceType == typeof(ISerializer<KeylessRegistration>))) {
            return provider.GetRequiredService<ISerializer<KeylessRegistration>>();
        }

        List<Type> serializerTypes = [.. services
            .Select(static sd => sd.ServiceType)
            .Where(static type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ISerializer<>))
            .Distinct()];

        if(serializerTypes.Count is not 1) {
            return null;
        }

        Type key = serializerTypes[0].GetGenericArguments()[0];
        Preca.ThrowIfFalse(
            typeof(ISerializerKey).IsAssignableFrom(key),
            () => new InvalidOperationException($"Registered serializer type '{key.FullName}' does not implement ISerializerKey and cannot be used as default."));

        return (ISerializer)provider.GetRequiredService(serializerTypes[0]);
    }

    public ISerializerConfigurator<TKey> ReplaceSerializer<TKey>(Func<IServiceProvider, ISerializer<TKey>> factory) where TKey : ISerializerKey {
        Preca.ThrowIfNull(factory);
        this.Services.RemoveAll<ISerializer<TKey>>();

        this.Services.AddSingleton<ISerializer<TKey>>(factory);

        return new SerializerConfigurator<TKey>(this);
    }

    public ISerializerConfigurator<KeylessRegistration> ReplaceSerializer(Func<IServiceProvider, ISerializer<KeylessRegistration>> factory) {
        return ReplaceSerializer<KeylessRegistration>(factory);
    }
}