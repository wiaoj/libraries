using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Threading.Channels;

namespace Wiaoj.Extensions.DependencyInjection;
/// <summary>
/// Provides extension methods for registering AsyncChannel services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class AsyncChannelServiceCollectionExtensions {
    /// <summary>
    /// Adds a singleton unbounded <see cref="AsyncChannel{T}"/> and its associated reader/writer to the services.
    /// </summary>
    /// <typeparam name="T">The type of data in the channel.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddUnboundedAsyncChannel<T>(this IServiceCollection services) {
        // TryAdd, aynı kanalın birden fazla kez kaydedilmesini önler.
        services.TryAddSingleton(AsyncChannel<T>.CreateUnbounded());

        // Okuyucu ve yazıcıyı, ana kanalı çözümleyerek singleton olarak kaydet.
        // Bu, farklı servislerin aynı kanalın okuyucusunu veya yazıcısını enjekte etmesine olanak tanır.
        services.TryAddSingleton(provider => provider.GetRequiredService<AsyncChannel<T>>().Reader);
        services.TryAddSingleton(provider => provider.GetRequiredService<AsyncChannel<T>>().Writer);

        return services;
    }

    /// <summary>
    /// Adds a singleton bounded <see cref="AsyncChannel{T}"/> and its associated reader/writer to the services.
    /// </summary>
    /// <typeparam name="T">The type of data in the channel.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="capacity">The maximum number of items the channel can store.</param>
    /// <param name="fullMode">The behavior to exhibit when writing to a full channel. Defaults to <see cref="BoundedChannelFullMode.Wait"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddBoundedAsyncChannel<T>(
        this IServiceCollection services,
        int capacity,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait) {

        BoundedChannelOptions options = new(capacity) {
            FullMode = fullMode,
            SingleReader = false, // DI senaryolarında birden fazla tüketici olabilir
            SingleWriter = false  // DI senaryolarında birden fazla üretici olabilir
        };

        return services.AddBoundedAsyncChannel<T>(options);
    }

    /// <summary>
    /// Adds a singleton bounded <see cref="AsyncChannel{T}"/> using the specified options.
    /// </summary>
    /// <typeparam name="T">The type of data in the channel.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="options">The options for configuring the bounded channel.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddBoundedAsyncChannel<T>(this IServiceCollection services, BoundedChannelOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        services.TryAddSingleton(AsyncChannel<T>.CreateBounded(options));
        services.TryAddSingleton(provider => provider.GetRequiredService<AsyncChannel<T>>().Reader);
        services.TryAddSingleton(provider => provider.GetRequiredService<AsyncChannel<T>>().Writer);

        return services;
    }
}