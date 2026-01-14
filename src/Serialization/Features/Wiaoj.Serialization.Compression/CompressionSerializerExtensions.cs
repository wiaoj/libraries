using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IO;
using System.IO.Compression;
using Wiaoj.Extensions.DependencyInjection;
using Wiaoj.Serialization.Compression;
using Wiaoj.Serialization.Compression.Abstractions;
using Wiaoj.Serialization.Compression.Compressors;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Serialization.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// Provides extension methods to add compression capabilities to a serializer configuration.
/// </summary> 
public static class CompressionSerializerExtensions {
    private static ISerializerConfigurator<TKey> AddCompression<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        Func<IServiceProvider, ICompressor> compressorFactory) where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);

        configurator.Builder.ConfigureServices(services => {
            services.TryAddSingleton<RecyclableMemoryStreamManager>();
            services.Decorate<ISerializer<TKey>>((innerSerializer, provider) => {
                return new CompressionSerializerDecorator<TKey>(
                    innerSerializer: innerSerializer,
                    compressor: compressorFactory(provider),
                    streamManager: provider.GetRequiredService<RecyclableMemoryStreamManager>()
                );
            });
        });

        return configurator;
    }

    /// <summary>
    /// Wraps the serializer with Brotli compression.
    /// </summary>
    public static ISerializerConfigurator<TKey> WithBrotliCompression<TKey>(
       this ISerializerConfigurator<TKey> configurator,
       CompressionLevel compressionLevel = CompressionLevel.Optimal) where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);

        return configurator.AddCompression(provider => {
            return new BrotliCompressor(
                compressionLevel,
                streamManager: provider.GetRequiredService<RecyclableMemoryStreamManager>());
        });
    }

    /// <summary>
    /// Wraps the serializer with Gzip compression.
    /// </summary>
    public static ISerializerConfigurator<TKey> WithGzipCompression<TKey>(
       this ISerializerConfigurator<TKey> configurator,
       CompressionLevel compressionLevel = CompressionLevel.Optimal) where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);

        return configurator.AddCompression(provider => {
            return new GzipCompressor(
                compressionLevel,
                streamManager: provider.GetRequiredService<RecyclableMemoryStreamManager>());
        });
    }
}