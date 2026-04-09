using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Serialization.DependencyInjection; 
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
        builder.Build();

        return services;
    }

    public static IServiceCollection AddWiaojSerializer(this IServiceCollection services) {
        return AddWiaojSerializer(services, (_) => { });
    }
}
