using Wiaoj.Serialization;

namespace Wiaoj.Webhooks;

/// <summary>
/// Serializer key used to (optionally) register a webhook-specific <see cref="ISerializer{TKey}"/>.
/// </summary>
/// <remarks>
/// If the host application registers a serializer under this key (e.g. via
/// <c>services.AddWiaojSerializer(s => s.UseSystemTextJson&lt;WebhookSerializerKey&gt;(...))</c>),
/// that serializer is used for every outbound webhook payload. If it never does,
/// <c>Wiaoj.Webhooks.DependencyInjection</c> registers a default JSON serializer under this key
/// via <c>TryUseSystemTextJson&lt;WebhookSerializerKey&gt;()</c>, so a serializer is always present.
/// </remarks>
public readonly struct WebhookSerializerKey : ISerializerKey;