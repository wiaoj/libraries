using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.DistributedCounter.Hosting;             // AutoFlushService burada
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.ObjectPool;
using Wiaoj.ObjectPool.Extensions;            // Factory ve Wrapper burada

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
public static class DistributedCounterServiceCollectionExtensions {
    public static IServiceCollection AddDistributedCounter(
        this IServiceCollection services,
        Action<IDistributedCounterBuilder> setupAction) {

        var options = new DistributedCounterOptions();
        var builder = new DistributedCounterBuilder(services, options);

        // Kullanıcının ayarlarını uygula
        setupAction(builder);

        // Options'ı DI container'a ekle
        services.TryAddSingleton(Options.Options.Create(options));

        // Key Builder: Varsayılan olarak DefaultCounterKeyBuilder'ı ekle.
        // TryAddSingleton kullandığımız için, eğer kullanıcı daha önce 
        // kendi ICounterKeyBuilder'ını eklediyse bizimki onu ezmez.
        services.TryAddSingleton<ICounterKeyBuilder, DefaultCounterKeyBuilder>();

        // Factory (Sayaçları üreten merkez)
        services.TryAddSingleton<DistributedCounterFactory>();
        services.TryAddSingleton<IDistributedCounterFactory>(sp => sp.GetRequiredService<DistributedCounterFactory>());

        // Background Service (Buffered sayaçları flush etmek için)
        // Microsoft.Extensions.Hosting.Abstractions paketi sayesinde çalışır
        services.AddHostedService<CounterAutoFlushService>();

        // Generic Typed Counters için açık generic kayıt (IDistributedCounter<T>)
        services.TryAddTransient(typeof(IDistributedCounter<>), typeof(TypedDistributedCounterWrapper<>));

        services.AddObjectPool<Dictionary<string, CounterValue>>(
            factory: () => new Dictionary<string, CounterValue>(StringComparer.Ordinal),
            resetter: dict => {
                dict.Clear(); 
                return true; 
            },
            configure: opt => {
                opt.MaximumRetained = 1024;
                opt.AccessMode = PoolAccessMode.FIFO;
            }
        );

        services.TryAddSingleton<IDistributedCounterService, DistributedCounterService>();
        return services;
    }

    public static IDistributedCounterBuilder Configure(
        this IDistributedCounterBuilder builder,
        Action<DistributedCounterOptions> configure) {
        configure(builder.Options);
        return builder;
    }
}