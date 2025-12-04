using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IO;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Wiaoj.Serialization.Abstractions;
using Wiaoj.Serialization.Bson;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Serialization.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// Extension methods to register BSON serializers in IWiaojSerializationBuilder.
/// </summary>
public static class BsonSerializerExtensions {
    static BsonSerializerExtensions() {
        try {

            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonSerializer.RegisterSerializer(new ObjectSerializer());
        }
        catch (BsonSerializationException) {
        }
    }
    /// <summary>
    /// Registers BSON as the default (keyless) serializer with default settings.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> UseBson(this IWiaojSerializationBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.UseBson<KeylessRegistration>(_ => { }, _ => { });
    }

    /// <summary>
    /// Registers BSON as the default (keyless) serializer with custom context configuration.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> UseBson(
         this IWiaojSerializationBuilder builder,
         Action<BsonSerializationContext.Builder>? serializationConfigurator = null,
         Action<BsonDeserializationContext.Builder>? deserializationConfigurator = null,
         BsonSerializationArgs serializationArgs = default) {
        return builder.UseBson<KeylessRegistration>(serializationConfigurator, deserializationConfigurator, serializationArgs);
    }

    /// <summary>
    /// Registers BSON as a named serializer for the given key type with default settings.
    /// </summary>
    public static ISerializerConfigurator<TKey> UseBson<TKey>(this IWiaojSerializationBuilder builder)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        return builder.UseBson<TKey>(_ => { }, _ => { });
    }

    /// <summary>
    /// Registers BSON as a named serializer for the given key type with custom context configuration. 
    /// </summary>
    public static ISerializerConfigurator<TKey> UseBson<TKey>(
        this IWiaojSerializationBuilder builder,
        Action<BsonSerializationContext.Builder>? serializationConfigurator = null,
        Action<BsonDeserializationContext.Builder>? deserializationConfigurator = null,
        BsonSerializationArgs serializationArgs = default)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);

        Action<BsonSerializationContext.Builder> finalSerializationConfig = serializationConfigurator ?? (_ => { });
        Action<BsonDeserializationContext.Builder> finalDeserializationConfig = deserializationConfigurator ?? (_ => { });
        builder.ConfigureServices(services => services.TryAddSingleton<RecyclableMemoryStreamManager>());
        return builder.AddSerializer(sp =>
            new BsonSerializer<TKey>(
                finalSerializationConfig,
                finalDeserializationConfig,
                serializationArgs,
                sp.GetRequiredService<RecyclableMemoryStreamManager>()
            ));
    }

    public static ISerializerConfigurator<TKey> UseBson<TKey>(
       this IWiaojSerializationBuilder builder,
       BsonSerializationArgs serializationArgs)
       where TKey : ISerializerKey {
        return builder.UseBson<TKey>(null, null, serializationArgs);
    }

    public static ISerializerConfigurator<KeylessRegistration> UseBson(
        this IWiaojSerializationBuilder builder,
        BsonSerializationArgs serializationArgs) {
        return builder.UseBson<KeylessRegistration>(null, null, serializationArgs);
    }
}