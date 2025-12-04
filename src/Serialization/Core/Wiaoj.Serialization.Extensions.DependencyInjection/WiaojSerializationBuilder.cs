using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Serialization.Abstractions;

namespace Wiaoj.Serialization.Extensions.DependencyInjection;
/// <inheritdoc /> 
internal sealed class WiaojSerializationBuilder : IWiaojSerializationBuilder {
    private readonly IServiceCollection services;
    public IServiceCollection Services => this.services;

    internal WiaojSerializationBuilder(IServiceCollection services) {
        this.services = services;
    }


    /// <inheritdoc /> 
    public ISerializerConfigurator<TKey> AddSerializer<TKey>(Func<IServiceProvider, ISerializer<TKey>> factory) where TKey : ISerializerKey {
        Preca.ThrowIfNull(factory);
        Type serviceType = typeof(ISerializer<TKey>);
        if (this.services.Any(sd => sd.ServiceType == serviceType)) {
            Preca.ThrowIfTrue(
                typeof(TKey) == typeof(KeylessRegistration),
                () => new InvalidOperationException("A keyless (default) serializer has already been registered. Only one keyless registration is allowed."));

            throw new InvalidOperationException($"A serializer with the key '{typeof(TKey).FullName}' has already been registered.");
        }

        this.services.AddSingleton<ISerializer<TKey>>(factory);
        return new SerializerConfigurator<TKey>(this);
    }

    /// <inheritdoc /> 
    public ISerializerConfigurator<KeylessRegistration> AddSerializer(Func<IServiceProvider, ISerializer<KeylessRegistration>> factory) {
        return AddSerializer<KeylessRegistration>(factory);
    }

    /// <summary>
    /// Finalizes the serializer configuration and registers a default <see cref="ISerializer"/> alias.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no serializers were registered, or if multiple serializers exist and no default was specified.
    /// </exception>
    internal void Build() {
        // Check for explicitly registered keyless (default) serializer
        bool hasKeyless = this.services.Any(sd => sd.ServiceType == typeof(ISerializer<KeylessRegistration>));

        if (hasKeyless) {
            this.services.AddSingleton<ISerializer>(sp => sp.GetRequiredService<ISerializer<KeylessRegistration>>());
            return;
        }

        // Gather all ISerializer<T> registrations
        List<ServiceDescriptor> serializerRegistrations = [.. this.services
            .Where(sd =>
                sd.ServiceType.IsGenericType &&
                sd.ServiceType.GetGenericTypeDefinition() == typeof(ISerializer<>))];

        // Use the only registered serializer as default if it exists and is valid
        if (serializerRegistrations.Count is 1) {
            Type genericArg = serializerRegistrations[0].ServiceType.GetGenericArguments()[0];

            Preca.ThrowIfFalse(
                typeof(ISerializerKey).IsAssignableFrom(genericArg),
                () => new InvalidOperationException($"Registered serializer type '{genericArg.FullName}' does not implement ISerializerKey and cannot be used as default."));

            Type serializerType = typeof(ISerializer<>).MakeGenericType(genericArg);

            this.services.AddSingleton(typeof(ISerializer), sp => sp.GetRequiredService(serializerType));
            return;
        }


        //Preca.ThrowIf(
        //    serializerRegistrations.Count > 1,
        //    () => new InvalidOperationException("Multiple serializers were registered, but no default (keyless) serializer was configured. Please register one using AddSerializer(...)."));

        //Preca.ThrowIf(
        //    serializerRegistrations.Count == 0,
        //    () => new InvalidOperationException("No serializers have been registered. Please call AddSerializer(...) first.")); 
    }
}