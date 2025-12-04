using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IO;
using Wiaoj.Serialization.Abstractions;
using Wiaoj.Serialization.Extensions.DependencyInjection;
using Wiaoj.Serialization.Security.Abstractions;
using Wiaoj.Serialization.Security.Extensions;

namespace Wiaoj.Serialization.Security;

public static class EncryptionSerializerExtensions {
    private static ISerializerConfigurator<TKey> AddAuthenticatedEncryption<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        Func<IServiceProvider, IAuthenticatedEncryptor> encryptorFactory) where TKey : ISerializerKey {
        IServiceCollection services = configurator.Builder.Services;
        ServiceDescriptor originalDescriptor = services.Last(d => d.ServiceType == typeof(ISerializer<TKey>));

        // Bu factory, hem iç serializer'ı hem de encryptor'ı DI'dan çözer.
        Func<IServiceProvider, AuthenticatedEncryptionSerializer<TKey>> decoratedFactory = provider => {
            // Orijinal serializer'ı oluşturan factory'yi al ve çalıştır.
            Func<IServiceProvider, object> originalFactory = originalDescriptor.ImplementationFactory
                ?? (sp => ActivatorUtilities.CreateInstance(sp, originalDescriptor.ImplementationType!));
            ISerializer<TKey> innerSerializer = (ISerializer<TKey>)originalFactory(provider);

            // Yeni encryptor'ı oluşturan factory'yi çalıştır.
            IAuthenticatedEncryptor encryptor = encryptorFactory(provider);

            // Decorator'ı tüm bağımlılıklarıyla oluştur.
            return new AuthenticatedEncryptionSerializer<TKey>(
                innerSerializer,
                encryptor,
                provider.GetRequiredService<RecyclableMemoryStreamManager>()
            );
        };

        services.Replace(ServiceDescriptor.Describe(originalDescriptor.ServiceType, decoratedFactory, originalDescriptor.Lifetime));
        return configurator;
    }

    /// <summary>
    /// Wraps the serializer with AES-GCM authenticated encryption using a directly provided key.
    /// </summary>
    /// <remarks>
    /// This encryption method is not supported on all platforms (e.g., Blazor WebAssembly).
    /// </remarks>
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [SupportedOSPlatform("ios13.0")]
    [SupportedOSPlatform("tvos13.0")]
    public static ISerializerConfigurator<TKey> WithAesGcmEncryption<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        byte[] key) where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);
        Preca.ThrowIfNull(key);
        Preca.Extensions.ThrowIfNotValidAesKeySize(key);
        ReadOnlyMemory<byte> keyMemory = new(key);
        return configurator.AddAuthenticatedEncryption(
            provider => new AesGcmEncryptor(
                keyMemory,
                provider.GetRequiredService<RecyclableMemoryStreamManager>()));
    }

    /// <summary>
    /// Wraps the serializer with AES-GCM authenticated encryption, reading the key from configuration.
    /// </summary>
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [SupportedOSPlatform("ios13.0")]
    [SupportedOSPlatform("tvos13.0")]
    public static ISerializerConfigurator<TKey> WithAesGcmEncryption<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        Func<IConfiguration, IConfigurationSection> configSelector) where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);
        Preca.ThrowIfNull(configSelector);

        return configurator.AddAuthenticatedEncryption(provider => {
            IConfiguration configuration = provider.GetRequiredService<IConfiguration>();
            IConfigurationSection section = configSelector(configuration);

            string? base64Key = section.Value;

            Preca.ThrowIfNullOrWhiteSpace(
                base64Key,
                () => new WiaojSecurityConfigurationException("Encryption key is missing or empty.", path: section.Path));

            byte[] keyBytes = new byte[GetDecodedLength(base64Key)];

            Preca.ThrowIfFalse(
                Convert.TryFromBase64String(base64Key, keyBytes, out int bytesWritten),
                () => new WiaojSecurityConfigurationException("The provided key is not a valid Base64 string.", path: section.Path));

            Preca.Extensions.ThrowIfNotValidAesKeySize(keyBytes);

            return new AesGcmEncryptor(
               new ReadOnlyMemory<byte>(keyBytes),
               provider.GetRequiredService<RecyclableMemoryStreamManager>()
           );
        });

        static int GetDecodedLength(string base64String) {
            return string.IsNullOrEmpty(base64String)
                ? 0
                : base64String.Length > 1 && base64String.EndsWith("==")
                ? (base64String.Length * 3 / 4) - 2
                : base64String.Length > 0 && base64String.EndsWith('=')
                ? (base64String.Length * 3 / 4) - 1
                : base64String.Length * 3 / 4;
        }
    }


    /// <summary>
    /// Wraps the serializer with AES-GCM authenticated encryption using a custom factory to resolve the key.
    /// </summary>
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [SupportedOSPlatform("ios13.0")]
    [SupportedOSPlatform("tvos13.0")]
    public static ISerializerConfigurator<TKey> WithAesGcmEncryption<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        Func<IServiceProvider, byte[]> keyFactory) where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);
        Preca.ThrowIfNull(keyFactory);
        return configurator.AddAuthenticatedEncryption(provider => {
            byte[] key = keyFactory(provider);
            Preca.ThrowIfNull(key);
            Preca.Extensions.ThrowIfNotValidAesKeySize(key);
            return new AesGcmEncryptor(
               new ReadOnlyMemory<byte>(key),
               provider.GetRequiredService<RecyclableMemoryStreamManager>()
           );
        });
    }

    public static ISerializerConfigurator<TKey> WithAesGcmEncryptionFromBase64<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        string base64Key) where TKey : ISerializerKey {
        Preca.ThrowIfNullOrWhiteSpace(base64Key);

        byte[] key;
        try {
            key = Convert.FromBase64String(base64Key);
        }
        catch (FormatException) {
            throw new WiaojSecurityConfigurationException("Invalid base64 encryption key provided.");
        }

        return configurator.WithAesGcmEncryption(key);
    }
}