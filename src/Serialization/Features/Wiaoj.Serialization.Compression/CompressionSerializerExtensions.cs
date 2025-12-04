using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IO;
using Wiaoj.Serialization.Abstractions;
using Wiaoj.Serialization.Compression.Abstractions;
using Wiaoj.Serialization.Compression.Compressors;
using Wiaoj.Serialization.Extensions.DependencyInjection;

namespace Wiaoj.Serialization.Compression;
/// <summary>
/// Provides extension methods to add compression capabilities to a serializer configuration.
/// </summary> 
public static class CompressionSerializerExtensions {
    private static ISerializerConfigurator<TKey> AddCompression<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        Func<IServiceProvider, ICompressor> compressorFactory) where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);

        IServiceCollection services = configurator.Builder.Services;
        ServiceDescriptor originalDescriptor = services.Last(d => d.ServiceType == typeof(ISerializer<TKey>));

        Func<IServiceProvider, object> decoratedFactory = provider => {
            Func<IServiceProvider, object> originalFactory = originalDescriptor.ImplementationFactory
                ?? (sp => ActivatorUtilities.CreateInstance(sp, originalDescriptor.ImplementationType!)); 

            return new CompressionSerializerDecorator<TKey>(
                innerSerializer: (ISerializer<TKey>)originalFactory(provider), 
                compressor: compressorFactory(provider), 
                streamManager: provider.GetRequiredService<RecyclableMemoryStreamManager>());
        };

        services.Replace(ServiceDescriptor.Describe(originalDescriptor.ServiceType, decoratedFactory, originalDescriptor.Lifetime));
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