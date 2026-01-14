namespace Wiaoj.Serialization.Transcoding.Abstractions;

public interface ITranscodingContext {
    /// <summary>
    /// Completes the transcoding process by serializing the intermediate object to the destination format.
    /// This is the most performant method.
    /// </summary>
    byte[] To<TDestinationKey, TModel>() where TDestinationKey : notnull, ISerializerKey;
}