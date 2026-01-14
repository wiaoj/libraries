namespace Wiaoj.Serialization.Transcoding.Abstractions;

public interface ITranscoder {
    /// <summary>
    /// Starts the transcoding process by specifying the source data and its format.
    /// </summary>
    ITranscodingContext From<TSourceKey>(byte[] sourceData) where TSourceKey : notnull, ISerializerKey;
}