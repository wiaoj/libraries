using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Serialization.Abstractions;
using Wiaoj.Serialization.Transcoding;
using Wiaoj.Serialization.Transcoding.Abstractions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Serialization.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
public static class TranscodingSerializerExtensions {
    /// <summary>
    /// Adds the ITranscoder service to the service collection, enabling fluent transcoding between formats.
    /// This should be called after AddWiaojSerializer.
    /// </summary>
    public static IWiaojSerializationBuilder AddTranscoding(this IWiaojSerializationBuilder builder) {
        return builder.ConfigureServices(services => services.TryAddSingleton<ITranscoder, Transcoder>());
    }
}