using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Extensions.DependencyInjection;
using Wiaoj.Security;
using Wiaoj.Serialization.Security;

namespace Wiaoj.Serialization;

/// <summary>Encrypts a serializer's output with a Wiaoj.Security key ring.</summary>
public static class EncryptionSerializerExtensions {
    /// <summary>
    /// Encrypts everything this serializer writes with the <see cref="ISecretProtector{TContext}"/> of
    /// <typeparamref name="TContext"/>: authenticated (AES-GCM), keyed by that context's key ring, and readable after the
    /// ring rotates for as long as it holds the older key.
    /// </summary>
    /// <typeparam name="TKey">The serializer key.</typeparam>
    /// <typeparam name="TContext">The secret context whose key ring encrypts the data.</typeparam>
    /// <param name="configurator">The serializer being configured.</param>
    /// <returns>The configurator, for chaining.</returns>
    /// <remarks>
    /// Requires an <see cref="ISecretProtector{TContext}"/> in the container, which
    /// <c>services.AddWiaojSecurity().AddManagedProtector&lt;TContext&gt;()</c> registers.
    /// <code>
    /// services.AddWiaojSecurity()
    ///     .AddEnvironmentMasterKey()
    ///     .AddEntityFrameworkKeyStore&lt;AppDbContext&gt;()
    ///     .AddManagedProtector&lt;CacheContext&gt;();
    ///
    /// services.AddWiaojSerializer(s => s
    ///     .UseMessagePack&lt;CacheKey&gt;()
    ///     .WithEncryption&lt;CacheKey, CacheContext&gt;());
    /// </code>
    /// </remarks>
    public static ISerializerConfigurator<TKey> WithEncryption<TKey, TContext>(this ISerializerConfigurator<TKey> configurator)
        where TKey : ISerializerKey
        where TContext : ISecretContext {
        Preca.ThrowIfNull(configurator);

        configurator.Builder.ConfigureServices(static services =>
            services.Decorate<ISerializer<TKey>>(static (inner, provider) =>
                new EncryptingSerializer<TKey, TContext>(inner, provider.GetRequiredService<ISecretProtector<TContext>>())));

        return configurator;
    }
}
