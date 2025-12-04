using Wiaoj.Serialization.Abstractions;                   
using Wiaoj.Serialization.Transcoding.Abstractions;

namespace Wiaoj.Serialization.Transcoding;
internal sealed class Transcoder(ISerializerProvider serializerProvider) : ITranscoder {
    public ITranscodingContext From<TSourceKey>(byte[] sourceData) where TSourceKey : notnull, ISerializerKey {
        return new TranscodingContext<TSourceKey>(sourceData, serializerProvider);
    }

    readonly struct TranscodingContext<TSourceKey>(byte[] sourceData, ISerializerProvider serializerProvider) : ITranscodingContext
        where TSourceKey : notnull, ISerializerKey {
        private readonly ISerializerProvider _serializerProvider = serializerProvider;

        public readonly byte[] To<TDestinationKey, TModel>() where TDestinationKey : notnull, ISerializerKey {
            ISerializer<TSourceKey> sourceSerializer = this._serializerProvider.GetSerializer<TSourceKey>()!;
            ISerializer<TDestinationKey> destinationSerializer = this._serializerProvider.GetSerializer<TDestinationKey>()!;

            TModel? model = sourceSerializer.Deserialize<TModel>(sourceData);

            return destinationSerializer.Serialize(model);
        }
    }
} 