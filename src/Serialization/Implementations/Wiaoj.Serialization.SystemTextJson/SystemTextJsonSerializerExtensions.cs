using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Wiaoj.Serialization.SystemTextJson;

#pragma warning disable IDE0130
namespace Wiaoj.Serialization.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods to register System.Text.Json serializers in ISerializationBuilder.
/// </summary>
public static class SystemTextJsonSerializerExtensions {
    /// <summary>
    /// Registers System.Text.Json as the default (keyless) serializer with default options.
    /// </summary>
    /// <param name="builder">The serialization builder.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<KeylessRegistration> UseSystemTextJson(this ISerializationBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.UseSystemTextJson(_ => { });
    }

    /// <summary>
    /// Registers System.Text.Json as the default (keyless) serializer with the specified options instance.
    /// </summary>
    /// <param name="builder">The serialization builder.</param>
    /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to use.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<KeylessRegistration> UseSystemTextJson(this ISerializationBuilder builder,
                                                              JsonSerializerOptions jsonSerializerOptions) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(jsonSerializerOptions);
        return builder.AddSerializer(sp => new SystemTextJsonSerializer<KeylessRegistration>(jsonSerializerOptions));
    }

    /// <summary>
    /// Registers System.Text.Json as the default (keyless) serializer with a specific <see cref="IJsonTypeInfoResolver"/> (such as a Source Generated <see cref="JsonSerializerContext"/>).
    /// </summary>
    /// <param name="builder">The serialization builder.</param>
    /// <param name="resolver">The type info resolver or source-generated context.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<KeylessRegistration> UseSystemTextJson(this ISerializationBuilder builder,
                                                              IJsonTypeInfoResolver resolver) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(resolver);
        JsonSerializerOptions options = new();
        options.TypeInfoResolverChain.Add(resolver);
        return builder.UseSystemTextJson(options);
    }

    /// <summary>
    /// Registers System.Text.Json as the default (keyless) serializer with custom configuration.
    /// </summary>
    /// <param name="builder">The serialization builder.</param>
    /// <param name="configure">An action to configure <see cref="JsonSerializerOptions"/>.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<KeylessRegistration> UseSystemTextJson(this ISerializationBuilder builder,
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
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(this ISerializationBuilder builder)
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
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(this ISerializationBuilder builder,
                                                                    JsonSerializerOptions jsonSerializerOptions)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(jsonSerializerOptions);
        return builder.AddSerializer(sp => new SystemTextJsonSerializer<TKey>(jsonSerializerOptions));
    }

    /// <summary>
    /// Registers System.Text.Json as a named serializer for the given key type with a specific <see cref="IJsonTypeInfoResolver"/> (such as a Source Generated <see cref="JsonSerializerContext"/>).
    /// </summary>
    /// <typeparam name="TKey">The serializer key type.</typeparam>
    /// <param name="builder">The serialization builder.</param>
    /// <param name="resolver">The type info resolver or source-generated context.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(this ISerializationBuilder builder,
                                                                    IJsonTypeInfoResolver resolver)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(resolver);
        JsonSerializerOptions options = new();
        options.TypeInfoResolverChain.Add(resolver);
        return builder.UseSystemTextJson<TKey>(options);
    }

    /// <summary>
    /// Registers System.Text.Json as a named serializer for the given key type with a Source Generated <see cref="JsonSerializerContext"/> for Native AOT.
    /// </summary>
    /// <typeparam name="TKey">The serializer key type.</typeparam>
    /// <typeparam name="TContext">The source generated <see cref="JsonSerializerContext"/> type.</typeparam>
    /// <param name="builder">The serialization builder.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey, TContext>(this ISerializationBuilder builder)
        where TKey : ISerializerKey
        where TContext : JsonSerializerContext, new() {
        Preca.ThrowIfNull(builder);
        return builder.UseSystemTextJson<TKey>(new TContext());
    }

    /// <summary>
    /// Registers System.Text.Json as a named serializer for the given key type with custom configuration.
    /// </summary>
    /// <typeparam name="TKey">The serializer key type.</typeparam>
    /// <param name="builder">The serialization builder.</param>
    /// <param name="configure">An action to configure <see cref="JsonSerializerOptions"/>.</param>
    /// <returns>The updated builder for chaining.</returns>
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(this ISerializationBuilder builder,
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
    public static ISerializerConfigurator<KeylessRegistration> TryUseSystemTextJson(this ISerializationBuilder builder) {
        Preca.ThrowIfNull(builder);
        return builder.TryUseSystemTextJson(_ => { });
    }

    /// <summary>
    /// Tries to register System.Text.Json with specific options.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseSystemTextJson(this ISerializationBuilder builder,
                                                              JsonSerializerOptions jsonSerializerOptions) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(jsonSerializerOptions);
        return builder.TryAddSerializer(sp => new SystemTextJsonSerializer<KeylessRegistration>(jsonSerializerOptions));
    }

    /// <summary>
    /// Tries to register System.Text.Json with a specific <see cref="IJsonTypeInfoResolver"/> (such as a Source Generated <see cref="JsonSerializerContext"/>).
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseSystemTextJson(this ISerializationBuilder builder,
                                                              IJsonTypeInfoResolver resolver) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(resolver);
        JsonSerializerOptions options = new();
        options.TypeInfoResolverChain.Add(resolver);
        return builder.TryUseSystemTextJson(options);
    }

    /// <summary>
    /// Tries to register System.Text.Json with configuration action.
    /// </summary>
    public static ISerializerConfigurator<KeylessRegistration> TryUseSystemTextJson(this ISerializationBuilder builder,
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
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey>(this ISerializationBuilder builder)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        return builder.TryUseSystemTextJson<TKey>(_ => { });
    }

    /// <summary>
    /// Tries to register System.Text.Json for a specific key with options.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey>(this ISerializationBuilder builder,
                                                                    JsonSerializerOptions jsonSerializerOptions)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(jsonSerializerOptions);
        return builder.TryAddSerializer(sp => new SystemTextJsonSerializer<TKey>(jsonSerializerOptions));
    }

    /// <summary>
    /// Tries to register System.Text.Json for a specific key with a specific <see cref="IJsonTypeInfoResolver"/> (such as a Source Generated <see cref="JsonSerializerContext"/>).
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey>(this ISerializationBuilder builder,
                                                                    IJsonTypeInfoResolver resolver)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(resolver);
        JsonSerializerOptions options = new();
        options.TypeInfoResolverChain.Add(resolver);
        return builder.TryUseSystemTextJson<TKey>(options);
    }

    /// <summary>
    /// Tries to register System.Text.Json for a specific key with a Source Generated <see cref="JsonSerializerContext"/> for Native AOT.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey, TContext>(this ISerializationBuilder builder)
        where TKey : ISerializerKey
        where TContext : JsonSerializerContext, new() {
        Preca.ThrowIfNull(builder);
        return builder.TryUseSystemTextJson<TKey>(new TContext());
    }

    /// <summary>
    /// Tries to register System.Text.Json for a specific key with configuration action.
    /// </summary>
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey>(this ISerializationBuilder builder,
                                                                    Action<JsonSerializerOptions> configure)
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        JsonSerializerOptions options = new();
        configure(options);
        return builder.TryAddSerializer(sp => new SystemTextJsonSerializer<TKey>(options));
    }

    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(this ISerializerConfigurator<TKey> configurator,
                                                                        Action<JsonSerializerOptions> configure) 
        where TKey : ISerializerKey {
        Preca.ThrowIfNull(configurator);
        Preca.ThrowIfNull(configure);
        JsonSerializerOptions options = new();
        configure(options);
         
        return configurator.Builder.ReplaceSerializer<TKey>(sp => new SystemTextJsonSerializer<TKey>(options));
    }
}