using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Serialization.DependencyInjection;

public static class WiaojBuilderExtensions {
    extension(IWiaojSerializationBuilder builder) {
        public IWiaojSerializationBuilder ConfigureServices(
            Action<IServiceCollection> configure) {
            // Pattern Matching ile kontrol ediyoruz
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
    }
}