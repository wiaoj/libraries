using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wiaoj.Serialization.DependencyInjection;
/// <inheritdoc /> 
internal sealed class WiaojSerializationBuilder : IWiaojSerializationBuilder, IServiceCollectionAccessor {
    public IServiceCollection Services { get; }

    internal WiaojSerializationBuilder(IServiceCollection services) {
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
    /// Finalizes the serializer configuration and registers a default <see cref="ISerializer"/> alias.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no serializers were registered, or if multiple serializers exist and no default was specified.
    /// </exception>
    internal void Build() {
        // Check for explicitly registered keyless (default) serializer
        bool hasKeyless = this.Services.Any(sd => sd.ServiceType == typeof(ISerializer<KeylessRegistration>));

        if(hasKeyless) {
            this.Services.AddSingleton<ISerializer>(sp => sp.GetRequiredService<ISerializer<KeylessRegistration>>());
            return;
        }

        // Gather all ISerializer<T> registrations
        List<ServiceDescriptor> serializerRegistrations = [.. this.Services
            .Where(sd =>
                sd.ServiceType.IsGenericType &&
                sd.ServiceType.GetGenericTypeDefinition() == typeof(ISerializer<>))];

        // Use the only registered serializer as default if it exists and is valid
        if(serializerRegistrations.Count is 1) {
            Type genericArg = serializerRegistrations[0].ServiceType.GetGenericArguments()[0];

            Preca.ThrowIfFalse(
                typeof(ISerializerKey).IsAssignableFrom(genericArg),
                () => new InvalidOperationException($"Registered serializer type '{genericArg.FullName}' does not implement ISerializerKey and cannot be used as default."));

            Type serializerType = typeof(ISerializer<>).MakeGenericType(genericArg);

            this.Services.AddSingleton(typeof(ISerializer), sp => sp.GetRequiredService(serializerType));
            return;
        }


        //Preca.ThrowIf(
        //    serializerRegistrations.Count > 1,
        //    () => new InvalidOperationException("Multiple serializers were registered, but no default (keyless) serializer was configured. Please register one using AddSerializer(...)."));

        //Preca.ThrowIf(
        //    serializerRegistrations.Count == 0,
        //    () => new InvalidOperationException("No serializers have been registered. Please call AddSerializer(...) first.")); 
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