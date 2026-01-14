namespace Wiaoj.Serialization;
/// <summary>
/// Builder for configuring and registering serializers with the dependency injection container.
/// </summary>
public interface IWiaojSerializationBuilder {
    /// <summary>
    /// Registers a serializer for the specified key type.
    /// </summary>
    /// <typeparam name="TKey">The serializer key type. Must implement <see cref="ISerializerKey"/>.</typeparam>
    /// <param name="factory">Factory function to create the serializer instance.</param>
    /// <returns>The updated <see cref="WiaojSerializationBuilder"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a serializer with the same key type has already been registered.
    /// </exception>
    ISerializerConfigurator<TKey> AddSerializer<TKey>(Func<IServiceProvider, ISerializer<TKey>> factory) where TKey : ISerializerKey;

    /// <summary>
    /// Registers a default (keyless) serializer.
    /// </summary>
    /// <param name="factory">Factory function to create the serializer instance.</param>
    /// <returns>The updated <see cref="IWiaojSerializationBuilder"/> for chaining.</returns>
    ISerializerConfigurator<KeylessRegistration> AddSerializer(Func<IServiceProvider, ISerializer<KeylessRegistration>> factory);

    /// <summary>
    /// Tries to register a serializer for the specified key type.
    /// If a serializer with the same key is already registered, this operation does nothing but still returns a configurator.
    /// </summary>
    /// <typeparam name="TKey">The serializer key type.</typeparam>
    /// <param name="factory">Factory function to create the serializer instance.</param>
    /// <returns>A configurator to continue chaining.</returns>
    ISerializerConfigurator<TKey> TryAddSerializer<TKey>(Func<IServiceProvider, ISerializer<TKey>> factory) where TKey : ISerializerKey;

    /// <summary>
    /// Tries to register a default (keyless) serializer.
    /// If a default serializer is already registered, this operation does nothing.
    /// </summary>
    ISerializerConfigurator<KeylessRegistration> TryAddSerializer(Func<IServiceProvider, ISerializer<KeylessRegistration>> factory);
}