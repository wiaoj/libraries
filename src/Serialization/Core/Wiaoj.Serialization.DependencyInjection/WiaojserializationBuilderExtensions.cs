using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Serialization.DependencyInjection.Internal;

namespace Wiaoj.Serialization.DependencyInjection;

public static class WiaojserializationBuilderExtensions {
    public static IWiaojSerializationBuilder AddSerializerProvider(this IWiaojSerializationBuilder builder) {
        builder.ConfigureServices(services => {
            services.TryAddSingleton<ISerializerProvider, SerializerProvider>();
        });
        return builder;
    }
}
