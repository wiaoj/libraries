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
    }
}
