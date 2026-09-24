using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Identifiers;

/// <summary>
/// Chooses the <see cref="IdCodec"/> registered by <c>AddIdentifiers</c>. The ready-made codecs are extension methods
/// over <see cref="UseCodec"/>: <c>UsePlainCodec</c>, <c>UseAesCodec</c> and, in Wiaoj.Identifiers.Security,
/// <c>UseKeyRingCodec</c>.
/// </summary>
public interface IIdentifiersBuilder {
    /// <summary>Gets the service collection.</summary>
    IServiceCollection Services { get; }

    /// <summary>Uses the codec <paramref name="factory"/> creates, such as one whose key comes from a key ring.</summary>
    /// <param name="factory">Creates the codec from the container.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>Choosing a codec again replaces the previous choice.</remarks>
    IIdentifiersBuilder UseCodec(Func<IServiceProvider, IdCodec> factory);
}
