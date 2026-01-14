using System.Text.Json;
using Wiaoj.Serialization.SystemTextJson;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Serialization.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// Extension methods to register System.Text.Json serializers in IWiaojSerializationBuilder.
/// </summary>
public static class SystemTextJsonSerializerExtensions {
    /// <summary>
    /// Registers System.Text.Json as the default (keyless) serializer with default options.
    /// </summary>
    /// <param name="builder">The serialization builder.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<KeylessRegistration> UseSystemTextJson(this IWiaojSerializationBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.UseSystemTextJson(_ => { });
    }

    /// <summary>
    /// Registers System.Text.Json as the default (keyless) serializer with the specified options instance.
    /// </summary>
    /// <param name="builder">The serialization builder.</param>
    /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to use.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<KeylessRegistration> UseSystemTextJson(this IWiaojSerializationBuilder builder,
                                                              JsonSerializerOptions jsonSerializerOptions) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(jsonSerializerOptions);
        return builder.AddSerializer(sp => new SystemTextJsonSerializer<KeylessRegistration>(jsonSerializerOptions));
    }

    /// <summary>
    /// Registers System.Text.Json as the default (keyless) serializer with custom configuration.
    /// </summary>
    /// <param name="builder">The serialization builder.</param>
    /// <param name="configure">An action to configure <see cref="JsonSerializerOptions"/>.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<KeylessRegistration> UseSystemTextJson(this IWiaojSerializationBuilder builder,
                                                              Action<JsonSerializerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        JsonSerializerOptions options = new();
        configure(options);
        return builder.AddSerializer(sp => new SystemTextJsonSerializer<KeylessRegistration>(options));
    }

    /// <summary>
    /// Registers System.Text.Json as a named serializer for the given key type with default options.
    /// </summary>
    /// <typeparam name="TKey">The serializer key type.</typeparam>
    /// <param name="builder">The serialization builder.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(this IWiaojSerializationBuilder builder)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        return builder.UseSystemTextJson<TKey>(_ => { });
    }

    /// <summary>
    /// Registers System.Text.Json as a named serializer for the given key type with the specified options instance.
    /// </summary>
    /// <typeparam name="TKey">The serializer key type.</typeparam>
    /// <param name="builder">The serialization builder.</param>
    /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to use.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(this IWiaojSerializationBuilder builder,
                                                                    JsonSerializerOptions jsonSerializerOptions)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(jsonSerializerOptions);
        return builder.AddSerializer(sp => new SystemTextJsonSerializer<TKey>(jsonSerializerOptions));
    }

    /// <summary>
    /// Registers System.Text.Json as a named serializer for the given key type with custom configuration.
    /// </summary>
    /// <typeparam name="TKey">The serializer key type.</typeparam>
    /// <param name="builder">The serialization builder.</param>
    /// <param name="configure">An action to configure <see cref="JsonSerializerOptions"/>.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(this IWiaojSerializationBuilder builder,
                                                                    Action<JsonSerializerOptions> configure)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        JsonSerializerOptions options = new();
        configure(options);
        return builder.AddSerializer(sp => new SystemTextJsonSerializer<TKey>(options));
    }

    /// <summary>
    /// Tries to register System.Text.Json as the default (keyless) serializer.
    /// If a default serializer exists, this operation does nothing.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseSystemTextJson(this IWiaojSerializationBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.TryUseSystemTextJson(_ => { });
    }

    /// <summary>
    /// Tries to register System.Text.Json with specific options.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseSystemTextJson(this IWiaojSerializationBuilder builder,
                                                              JsonSerializerOptions jsonSerializerOptions) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(jsonSerializerOptions);
        return builder.TryAddSerializer(sp => new SystemTextJsonSerializer<KeylessRegistration>(jsonSerializerOptions));
    }

    /// <summary>
    /// Tries to register System.Text.Json with configuration action.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseSystemTextJson(this IWiaojSerializationBuilder builder,
                                                              Action<JsonSerializerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        JsonSerializerOptions options = new();
        configure(options);
        return builder.TryAddSerializer(sp => new SystemTextJsonSerializer<KeylessRegistration>(options));
    }

    /// <summary>
    /// Tries to register System.Text.Json for a specific key.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey>(this IWiaojSerializationBuilder builder)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        return builder.TryUseSystemTextJson<TKey>(_ => { });
    }

    /// <summary>
    /// Tries to register System.Text.Json for a specific key with options.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey>(this IWiaojSerializationBuilder builder,
                                                                    JsonSerializerOptions jsonSerializerOptions)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(jsonSerializerOptions);
        return builder.TryAddSerializer(sp => new SystemTextJsonSerializer<TKey>(jsonSerializerOptions));
    }

    /// <summary>
    /// Tries to register System.Text.Json for a specific key with configuration action.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey>(this IWiaojSerializationBuilder builder,
                                                                    Action<JsonSerializerOptions> configure)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        JsonSerializerOptions options = new();
        configure(options);
        return builder.TryAddSerializer(sp => new SystemTextJsonSerializer<TKey>(options));
    }
}