using MessagePack;
using MessagePack.Resolvers;
using Wiaoj.Serialization.Abstractions;
using Wiaoj.Serialization.Extensions.DependencyInjection;

namespace Wiaoj.Serialization.MessagePack;
/// <summary>
/// Extension methods to register MessagePack serializers in IWiaojSerializationBuilder.
/// </summary>
public static class MessagePackSerializerExtensions {
    private static MessagePackSerializerOptions DefaultOptions => MessagePackSerializerOptions.Standard
        .WithResolver(
            CompositeResolver.Create(
                ContractlessStandardResolver.Instance,
                NativeDecimalResolver.Instance,
                NativeGuidResolver.Instance,
                NativeDateTimeResolver.Instance,
                StandardResolver.Instance
            ))
        .WithSecurity(MessagePackSecurity.UntrustedData);

    /// <summary>
    /// Registers MessagePack as the default (keyless) serializer with default options.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> UseMessagePack(this IWiaojSerializationBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.UseMessagePack(_ => { });
    }

    /// <summary>
    /// Registers MessagePack as the default (keyless) serializer with a provided options instance.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> UseMessagePack(this IWiaojSerializationBuilder builder,
                                                           MessagePackSerializerOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        return builder.AddSerializer(sp => new MessagePackSerializer<KeylessRegistration>(options));
    }

    /// <summary>
    /// Registers MessagePack as the default (keyless) serializer with custom configuration.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> UseMessagePack(this IWiaojSerializationBuilder builder,
                                                           Action<MessagePackSerializerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        MessagePackSerializerOptions options = DefaultOptions;
        configure(options);
        return builder.AddSerializer(sp => new MessagePackSerializer<KeylessRegistration>(options));
    }

    /// <summary>
    /// Registers MessagePack as a named serializer for the given key type with default options.
    /// </summary>
    public static ISerializerConfigurator<TKey> UseMessagePack<TKey>(this IWiaojSerializationBuilder builder)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        return builder.UseMessagePack<TKey>(_ => { });
    }

    /// <summary>
    /// Registers MessagePack as a named serializer for the given key type with a provided options instance.
    /// </summary>
    public static ISerializerConfigurator<TKey> UseMessagePack<TKey>(this IWiaojSerializationBuilder builder,
                                                                 MessagePackSerializerOptions options)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        return builder.AddSerializer(sp => new MessagePackSerializer<TKey>(options));
    }

    /// <summary>
    /// Registers MessagePack as a named serializer for the given key type with custom configuration.
    /// </summary>
    public static ISerializerConfigurator<TKey> UseMessagePack<TKey>(this IWiaojSerializationBuilder builder,
                                                                 Action<MessagePackSerializerOptions> configure)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        MessagePackSerializerOptions options = DefaultOptions;
        configure(options);
        return builder.AddSerializer(sp => new MessagePackSerializer<TKey>(options));
    }
}