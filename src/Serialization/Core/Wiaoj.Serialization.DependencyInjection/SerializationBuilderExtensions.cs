using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Serialization.DependencyInjection;
using Wiaoj.Serialization.DependencyInjection.Internal;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Serialization;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Core builder extensions for <see cref="ISerializationBuilder"/>.
/// </summary>
public static class SerializationBuilderExtensions {
    extension(ISerializationBuilder builder) {
        /// <summary>
        /// Configures the underlying service collection.
        /// </summary>
        public ISerializationBuilder ConfigureServices(
            Action<IServiceCollection> configure) {
            if(builder is IServiceCollectionAccessor accessor) {
                configure(accessor.Services);
            }
            else {
                throw new InvalidOperationException("This builder implementation does not support direct service configuration.");
            }

            return builder;
        }

        /// <summary>
        /// Belirli bir Key için var olan kaydı geçersiz kılmak (override) için bir giriş noktası sağlar.
        /// </summary>
        public ISerializerConfigurator<TKey> Override<TKey>() where TKey : ISerializerKey {
            return new SerializerConfigurator<TKey>(builder);
        }

        /// <summary>
        /// Adds the default serializer provider implementation to the builder.
        /// </summary>
        public ISerializationBuilder AddSerializerProvider() {
            builder.ConfigureServices(services => {
                services.TryAddSingleton<ISerializerProvider, SerializerProvider>();
            });
            return builder;
        }
    }
}