namespace Wiaoj.Serialization.Abstractions;
/// <summary>
/// Configures a serializer registration for a specific key and provides methods for chaining.
/// </summary>
/// <typeparam name="TKey">The key type of the serializer being configured.</typeparam>
public interface ISerializerConfigurator<out TKey> where TKey : ISerializerKey {
    /// <summary>
    /// Gets the main builder to continue the registration chain.
    /// </summary>
    IWiaojSerializationBuilder Builder { get; }
}