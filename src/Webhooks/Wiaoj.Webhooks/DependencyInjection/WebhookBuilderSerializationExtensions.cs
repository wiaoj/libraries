using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using Wiaoj.Serialization;
using Wiaoj.Serialization.DependencyInjection;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring JSON and binary payload serializers on <see cref="IWebhookBuilder"/>.
/// </summary>
public static class WebhookBuilderSerializationExtensions {
    /// <summary>
    /// Configures <see cref="JsonSerializerOptions"/> for the webhook payload serializer (<see cref="WebhookSerializerKey"/>).
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="configure">The delegate used to configure <see cref="JsonSerializerOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder ConfigureJsonSerializer(
        this IWebhookBuilder builder,
        Action<JsonSerializerOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        builder.Services.AddWiaojSerializer(serialization => {
            serialization.ReplaceSystemTextJson<WebhookSerializerKey>(configure);
        });

        return builder;
    }

    /// <summary>
    /// Configures snake_case property naming policy for inbound and outbound webhook JSON serialization.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseSnakeCaseJson(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);

        return builder.ConfigureJsonSerializer(options => {
            options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.PropertyNameCaseInsensitive = true;
        });
    }

    /// <summary>
    /// Configures camelCase property naming policy for inbound and outbound webhook JSON serialization.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseCamelCaseJson(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);

        return builder.ConfigureJsonSerializer(options => {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
        });
    }

    /// <summary>
    /// Registers a custom payload serializer implementation for <see cref="WebhookSerializerKey"/>.
    /// </summary>
    /// <typeparam name="TSerializer">The type implementing <see cref="ISerializer{WebhookSerializerKey}"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseSerializer<TSerializer>(this IWebhookBuilder builder)
        where TSerializer : class, ISerializer<WebhookSerializerKey> {
        Preca.ThrowIfNull(builder);

        builder.Services.RemoveAll<ISerializer<WebhookSerializerKey>>();
        builder.Services.AddSingleton<ISerializer<WebhookSerializerKey>, TSerializer>();

        return builder;
    }
}