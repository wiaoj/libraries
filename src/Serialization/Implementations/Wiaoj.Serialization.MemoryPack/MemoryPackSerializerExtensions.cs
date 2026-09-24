using MemoryPack;
using Wiaoj.Preconditions;
using Wiaoj.Serialization.MemoryPack;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Serialization;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods to register and replace MemoryPack serializers in <see cref="ISerializationBuilder"/>.
/// </summary>
public static class MemoryPackSerializerExtensions {
    private static MemoryPackSerializerOptions DefaultOptions => MemoryPackSerializerOptions.Default;

    /// <summary>
    /// Registers MemoryPack as the default (keyless) serializer with default options.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> UseMemoryPack(this ISerializationBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.UseMemoryPack(_ => { });
    }

    /// <summary>
    /// Registers MemoryPack as the default (keyless) serializer with a provided options instance.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> UseMemoryPack(
        this ISerializationBuilder builder,
        MemoryPackSerializerOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        return builder.AddSerializer(sp => new MemoryPackSerializer<KeylessRegistration>(options));
    }

    /// <summary>
    /// Registers MemoryPack as the default (keyless) serializer with custom configuration.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> UseMemoryPack(
        this ISerializationBuilder builder,
        Action<MemoryPackSerializerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        MemoryPackSerializerOptions options = DefaultOptions;
        configure(options);
        return builder.AddSerializer(sp => new MemoryPackSerializer<KeylessRegistration>(options));
    }

    /// <summary>
    /// Registers MemoryPack as a named serializer for the given key type with default options.
    /// </summary>
    public static ISerializerConfigurator<TKey> UseMemoryPack<TKey>(this ISerializationBuilder builder)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        return builder.UseMemoryPack<TKey>(_ => { });
    }

    /// <summary>
    /// Registers MemoryPack as a named serializer for the given key type with a provided options instance.
    /// </summary>
    public static ISerializerConfigurator<TKey> UseMemoryPack<TKey>(
        this ISerializationBuilder builder,
        MemoryPackSerializerOptions options)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        return builder.AddSerializer(sp => new MemoryPackSerializer<TKey>(options));
    }

    /// <summary>
    /// Registers MemoryPack as a named serializer for the given key type with custom configuration.
    /// </summary>
    public static ISerializerConfigurator<TKey> UseMemoryPack<TKey>(
        this ISerializationBuilder builder,
        Action<MemoryPackSerializerOptions> configure)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        MemoryPackSerializerOptions options = DefaultOptions;
        configure(options);
        return builder.AddSerializer(sp => new MemoryPackSerializer<TKey>(options));
    }

    /// <summary>
    /// Tries to register MemoryPack as the default (keyless) serializer.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseMemoryPack(this ISerializationBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.TryUseMemoryPack(_ => { });
    }

    /// <summary>
    /// Tries to register MemoryPack as the default (keyless) serializer with options.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseMemoryPack(
        this ISerializationBuilder builder,
        MemoryPackSerializerOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        return builder.TryAddSerializer(sp => new MemoryPackSerializer<KeylessRegistration>(options));
    }

    /// <summary>
    /// Tries to register MemoryPack as the default (keyless) serializer with configuration.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseMemoryPack(
        this ISerializationBuilder builder,
        Action<MemoryPackSerializerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        MemoryPackSerializerOptions options = DefaultOptions;
        configure(options);
        return builder.TryAddSerializer(sp => new MemoryPackSerializer<KeylessRegistration>(options));
    }

    /// <summary>
    /// Tries to register MemoryPack as a named serializer.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseMemoryPack<TKey>(this ISerializationBuilder builder)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        return builder.TryUseMemoryPack<TKey>(_ => { });
    }

    /// <summary>
    /// Tries to register MemoryPack as a named serializer with options.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseMemoryPack<TKey>(
        this ISerializationBuilder builder,
        MemoryPackSerializerOptions options)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        return builder.TryAddSerializer(sp => new MemoryPackSerializer<TKey>(options));
    }

    /// <summary>
    /// Tries to register MemoryPack as a named serializer with configuration.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseMemoryPack<TKey>(
        this ISerializationBuilder builder,
        Action<MemoryPackSerializerOptions> configure)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        MemoryPackSerializerOptions options = DefaultOptions;
        configure(options);
        return builder.TryAddSerializer(sp => new MemoryPackSerializer<TKey>(options));
    }

    /// <summary>
    /// Replaces the current serializer configuration for <typeparamref name="TKey"/> with MemoryPack.
    /// </summary>
    public static ISerializerConfigurator<TKey> UseMemoryPack<TKey>(
        this ISerializerConfigurator<TKey> configurator,
        Action<MemoryPackSerializerOptions>? configure = null) where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);
        MemoryPackSerializerOptions options = DefaultOptions;
        configure?.Invoke(options);
        return configurator.Builder.ReplaceSerializer<TKey>(sp => new MemoryPackSerializer<TKey>(options));
    }

    /// <summary>
    /// Replaces any existing serializer for <typeparamref name="TKey"/> with a configured <see cref="MemoryPackSerializer{TKey}"/>.
    /// </summary>
    public static ISerializerConfigurator<TKey> ReplaceMemoryPack<TKey>(
        this ISerializationBuilder builder,
        Action<MemoryPackSerializerOptions> configure)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        MemoryPackSerializerOptions options = DefaultOptions;
        configure(options);

        return builder.ReplaceSerializer<TKey>(sp => new MemoryPackSerializer<TKey>(options));
    }

    /// <summary>
    /// Replaces any existing serializer for <typeparamref name="TKey"/> with a configured <see cref="MemoryPackSerializer{TKey}"/>.
    /// </summary>
    public static ISerializerConfigurator<TKey> ReplaceMemoryPack<TKey>(
        this ISerializerConfigurator<TKey> builder,
        Action<MemoryPackSerializerOptions> configure)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        MemoryPackSerializerOptions options = DefaultOptions;
        configure(options);

        return builder.Builder.ReplaceSerializer<TKey>(sp => new MemoryPackSerializer<TKey>(options));
    }

    /// <summary>
    /// Replaces any existing serializer for <typeparamref name="TKey"/> with the specified <see cref="MemoryPackSerializerOptions"/> instance.
    /// </summary>
    public static ISerializerConfigurator<TKey> ReplaceMemoryPack<TKey>(
        this ISerializerConfigurator<TKey> builder,
        MemoryPackSerializerOptions memoryPackSerializerOptions)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(memoryPackSerializerOptions);

        return builder.Builder.ReplaceSerializer<TKey>(sp => new MemoryPackSerializer<TKey>(memoryPackSerializerOptions));
    }
}