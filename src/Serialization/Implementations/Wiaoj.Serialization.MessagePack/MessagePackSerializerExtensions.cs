using MessagePack;
using MessagePack.Resolvers;
using Wiaoj.Serialization.MessagePack;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Serialization.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
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

    /// <summary>
    /// Tries to register MessagePack as the default (keyless) serializer.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseMessagePack(this IWiaojSerializationBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.TryUseMessagePack(_ => { });
    }

    /// <summary>
    /// Tries to register MessagePack as the default (keyless) serializer with options.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseMessagePack(this IWiaojSerializationBuilder builder,
                                                           MessagePackSerializerOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        return builder.TryAddSerializer(sp => new MessagePackSerializer<KeylessRegistration>(options));
    }

    /// <summary>
    /// Tries to register MessagePack as the default (keyless) serializer with configuration.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseMessagePack(this IWiaojSerializationBuilder builder,
                                                           Action<MessagePackSerializerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        MessagePackSerializerOptions options = DefaultOptions;
        configure(options);
        return builder.TryAddSerializer(sp => new MessagePackSerializer<KeylessRegistration>(options));
    }

    /// <summary>
    /// Tries to register MessagePack as a named serializer.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseMessagePack<TKey>(this IWiaojSerializationBuilder builder)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        return builder.TryUseMessagePack<TKey>(_ => { });
    }

    /// <summary>
    /// Tries to register MessagePack as a named serializer with options.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseMessagePack<TKey>(this IWiaojSerializationBuilder builder,
                                                                 MessagePackSerializerOptions options)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        return builder.TryAddSerializer(sp => new MessagePackSerializer<TKey>(options));
    }

    /// <summary>
    /// Tries to register MessagePack as a named serializer with configuration.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseMessagePack<TKey>(this IWiaojSerializationBuilder builder,
                                                                 Action<MessagePackSerializerOptions> configure)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        MessagePackSerializerOptions options = DefaultOptions;
        configure(options);
        return builder.TryAddSerializer(sp => new MessagePackSerializer<TKey>(options));
    }
}