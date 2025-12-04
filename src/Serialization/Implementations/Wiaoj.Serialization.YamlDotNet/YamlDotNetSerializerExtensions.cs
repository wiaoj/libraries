using Wiaoj.Serialization.Abstractions;
using Wiaoj.Serialization.YamlDotNet;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Serialization.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
public static class YamlDotNetSerializerExtensions {
    /// <summary>
    /// Registers YamlDotNet as a serializer for the given key type.
    /// </summary>
    /// <typeparam name="TKey">The key to associate the serializer with.</typeparam>
    /// <param name="builder">The serialization builder.</param>
    /// <param name="configureSerializer">An action to configure the <see cref="SerializerBuilder"/>.</param>
    /// <param name="configureDeserializer">An action to configure the <see cref="DeserializerBuilder"/>.</param>
    /// <returns>The <see cref="IWiaojSerializationBuilder"/> for chaining.</returns>
    public static ISerializerConfigurator<TKey> UseYamlDotNet<TKey>(
        this IWiaojSerializationBuilder builder,
        Action<SerializerBuilder>? configureSerializer = null,
        Action<DeserializerBuilder>? configureDeserializer = null) where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);

        return builder.AddSerializer(sp => {
            SerializerBuilder serializerBuilder = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance);

            DeserializerBuilder deserializerBuilder = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance);

            configureSerializer?.Invoke(serializerBuilder);
            configureDeserializer?.Invoke(deserializerBuilder);

            return new YamlDotNetSerializer<TKey>(
                serializerBuilder.Build(),
                deserializerBuilder.Build()
            );
        });
    }

    /// <summary>
    /// Registers YamlDotNet as the default (keyless) serializer.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> UseYamlDotNet(
        this IWiaojSerializationBuilder builder,
        Action<SerializerBuilder>? configureSerializer = null,
        Action<DeserializerBuilder>? configureDeserializer = null) {
        return builder.UseYamlDotNet<KeylessRegistration>(configureSerializer, configureDeserializer);
    }
}