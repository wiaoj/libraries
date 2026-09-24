using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Serialization;
using Wiaoj.Serialization.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Service collection extension methods for registering Wiaoj serialization infrastructure.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds Wiaoj serializer support and returns a builder to register serializers on.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The serialization builder.</returns>
    /// <remarks>
    /// The non-keyed <see cref="ISerializer"/> is chosen when it is first resolved: the keyless serializer when one is
    /// registered, otherwise the only registered serializer. With several and no keyless one, there is none.
    /// </remarks>
    public static ISerializationBuilder AddWiaojSerializer(this IServiceCollection services) {
        Preca.ThrowIfNull(services);

        SerializationBuilder builder = new(services);
        builder.AddSerializerProvider();
        services.TryAddSingleton<ISerializer>(provider => SerializationBuilder.ResolveDefault(provider, services)!);

        return builder;
    }

    /// <summary>
    /// Adds Wiaoj serializer support and registers serializers inside <paramref name="configurationBuilder"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configurationBuilder">Registers serializers on the builder.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddWiaojSerializer(this IServiceCollection services, Action<ISerializationBuilder> configurationBuilder) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configurationBuilder);

        configurationBuilder(services.AddWiaojSerializer());
        return services;
    }
}
