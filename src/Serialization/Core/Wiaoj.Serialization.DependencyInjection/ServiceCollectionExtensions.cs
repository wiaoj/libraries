using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Preconditions;
using Wiaoj.Serialization;
using Wiaoj.Serialization.DependencyInjection;
using Wiaoj.Serialization.DependencyInjection.Internal;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Service collection extension methods for registering Wiaoj serialization infrastructure.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds Wiaoj serializer support to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configurationBuilder">A delegate to configure serializers using <see cref="SerializationBuilder"/>.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddWiaojSerializer(this IServiceCollection services, Action<ISerializationBuilder> configurationBuilder) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configurationBuilder);
        SerializationBuilder builder = new(services);
        configurationBuilder(builder);
        builder.AddSerializerProvider();
        builder.Build();

        return services;
    }

    /// <summary>
    /// Adds Wiaoj serializer support to the service collection with default configuration.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddWiaojSerializer(this IServiceCollection services) {
        return AddWiaojSerializer(services, (_) => { });
    }
}
