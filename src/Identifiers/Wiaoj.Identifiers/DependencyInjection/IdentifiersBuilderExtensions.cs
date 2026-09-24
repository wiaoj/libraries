using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wiaoj.Preconditions;

namespace Wiaoj.Identifiers;

/// <summary>The codecs Wiaoj.Identifiers provides, chosen on an <see cref="IIdentifiersBuilder"/>.</summary>
public static class IdentifiersBuilderExtensions {
    /// <summary>
    /// Uses <see cref="PlainIdCodec"/>: the Snowflake value in base62. Readable without a key, and reveals when each
    /// identifier was created.
    /// </summary>
    /// <param name="builder">The identifiers builder.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IIdentifiersBuilder UsePlainCodec(this IIdentifiersBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.UseCodec(static _ => PlainIdCodec.Instance);
    }

    /// <summary>
    /// Uses <see cref="AesIdCodec"/> with the key in <see cref="IdentifiersOptions"/> — typically bound from
    /// configuration, with the key kept in a secret store.
    /// </summary>
    /// <param name="builder">The identifiers builder.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>A missing or short key fails at startup; there is no default key.</remarks>
    public static IIdentifiersBuilder UseAesCodec(this IIdentifiersBuilder builder) {
        return builder.UseAesCodec(static _ => { });
    }

    /// <summary>Uses <see cref="AesIdCodec"/> with the key <paramref name="configure"/> sets.</summary>
    /// <param name="builder">The identifiers builder.</param>
    /// <param name="configure">Sets <see cref="IdentifiersOptions.AesKey"/> and optionally the version.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IIdentifiersBuilder UseAesCodec(this IIdentifiersBuilder builder, Action<IdentifiersOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<IdentifiersOptions>, IdentifiersOptionsValidator>());
        builder.Services.AddOptions<IdentifiersOptions>().Configure(configure).ValidateOnStart();

        return builder.UseCodec(static services => {
            IdentifiersOptions options = services.GetRequiredService<IOptions<IdentifiersOptions>>().Value;
            return new AesIdCodec(Convert.FromBase64String(options.AesKey!), options.AesKeyVersion);
        });
    }
}
