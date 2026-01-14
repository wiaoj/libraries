namespace Wiaoj.Serialization;
/// <summary>
/// Marker type for registering a default (keyless) serializer.
/// </summary>
public readonly struct KeylessRegistration : ISerializerKey;