using Wiaoj.Serialization.Abstractions;

namespace Wiaoj.Ddd.EntityFrameworkCore;     
/// <summary>
/// A marker key to isolate the serializer used specifically for EF Core Outbox messages.
/// This prevents conflicts with other serializers (e.g. Cache, API) in the application.
/// </summary>
public readonly struct DddEfCoreOutboxSerializerKey : ISerializerKey;