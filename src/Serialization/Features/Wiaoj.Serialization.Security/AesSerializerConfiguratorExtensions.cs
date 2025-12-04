using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IO;
using Wiaoj.Extensions.DependencyInjection;
using Wiaoj.Primitives;
using Wiaoj.Serialization.Abstractions;
using Wiaoj.Serialization.DependencyInjection;
using Wiaoj.Serialization.Security.Abstractions;
using Wiaoj.Serialization.Security.Extensions;

namespace Wiaoj.Serialization.Security;           
public static class EncryptionSerializerExtensions {
    private static ISerializerConfigurator<TKey> AddAuthenticatedEncryption<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        Func<IServiceProvider, IAuthenticatedEncryptor> encryptorFactory) where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);

        configurator.Builder.ConfigureServices(services => {
            services.TryAddSingleton<RecyclableMemoryStreamManager>();
            services.Decorate<ISerializer<TKey>>((innerSerializer, provider) => {
                IAuthenticatedEncryptor encryptor = encryptorFactory(provider);
                RecyclableMemoryStreamManager streamManager = provider.GetRequiredService<RecyclableMemoryStreamManager>();

                return new AuthenticatedEncryptionSerializer<TKey>(
                    innerSerializer,
                    encryptor,
                    streamManager
                );
            });
        });

        return configurator;
    }

    /// <summary>
    /// Wraps the serializer with AES-GCM authenticated encryption using a secure Secret key.
    /// </summary>
    [UnsupportedOSPlatform("browser")]
    [SupportedOSPlatform("ios13.0")]
    [SupportedOSPlatform("tvos13.0")]
    public static ISerializerConfigurator<TKey> WithAesGcmEncryption<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        Secret<byte> key) where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);

        key.Expose(k => Preca.Extensions.ThrowIfNotValidAesKeySize(k));

        return configurator.AddAuthenticatedEncryption(provider =>
            new AesGcmEncryptor(
                key,
                provider.GetRequiredService<RecyclableMemoryStreamManager>()
            ));
    }

    /// <summary>
    /// Wraps the serializer with AES-GCM authenticated encryption from configuration.
    /// </summary>
    [UnsupportedOSPlatform("browser")]
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

            string? rawValue = section.Value;

            Preca.ThrowIfNullOrWhiteSpace(
                rawValue,
                () => new WiaojSecurityConfigurationException("Encryption key is missing or empty.", path: section.Path));

            Base64String base64Key;
            try {
                base64Key = Base64String.Parse(rawValue);
            }
            catch (FormatException) {
                throw new WiaojSecurityConfigurationException("The provided key is not a valid Base64 string.", path: section.Path);
            }

            Secret<byte> secretKey = Secret.From(base64Key);

            secretKey.Expose(k => Preca.Extensions.ThrowIfNotValidAesKeySize(k));

            return new AesGcmEncryptor(
               secretKey,
               provider.GetRequiredService<RecyclableMemoryStreamManager>()
           );
        });
    }

    /// <summary>
    /// Wraps the serializer with AES-GCM encryption using a factory that resolves a Secret key.
    /// </summary>
    [UnsupportedOSPlatform("browser")]
    [SupportedOSPlatform("ios13.0")]
    [SupportedOSPlatform("tvos13.0")]
    public static ISerializerConfigurator<TKey> WithAesGcmEncryption<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        Func<IServiceProvider, Secret<byte>> keyFactory) where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);
        Preca.ThrowIfNull(keyFactory);

        return configurator.AddAuthenticatedEncryption(provider => {
            Secret<byte> key = keyFactory(provider);

            if (key.Length == 0) {
                throw new ArgumentException("Encryption key cannot be empty.");
            }

            key.Expose(k => Preca.Extensions.ThrowIfNotValidAesKeySize(k));

            return new AesGcmEncryptor(
               key,
               provider.GetRequiredService<RecyclableMemoryStreamManager>()
           );
        });
    }

    [UnsupportedOSPlatform("browser")]
    [SupportedOSPlatform("ios13.0")]
    [SupportedOSPlatform("tvos13.0")]
    public static ISerializerConfigurator<TKey> WithAesGcmEncryptionFromBase64<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        Base64String base64Key) where TKey : ISerializerKey {
        Secret<byte> key = Secret.From(base64Key);
        return configurator.WithAesGcmEncryption(key);
    }
}