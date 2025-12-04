using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Serialization.Extensions.DependencyInjection;
using Wiaoj.Serialization.Transcoding.Abstractions;

namespace Wiaoj.Serialization.Transcoding;
public static class TranscodingSerializerExtensions {
    /// <summary>
    /// Adds the ITranscoder service to the service collection, enabling fluent transcoding between formats.
    /// This should be called after AddWiaojSerializer.
    /// </summary>
    public static IWiaojSerializationBuilder AddTranscoding(this IWiaojSerializationBuilder builder) {
        builder.Services.TryAddSingleton<ITranscoder, Transcoder>();
        return builder;
    }
}