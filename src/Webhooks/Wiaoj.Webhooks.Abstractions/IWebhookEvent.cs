namespace Wiaoj.Webhooks;

/// <summary>
/// Marks a type as a webhook event that can be dispatched through the webhook pipeline.
/// </summary>
/// <remarks>
/// Implementations should be immutable and are typically declared as <see langword="sealed record"/> types.
/// No members are required by this contract — event identification, serialization, and signing
/// are handled by other collaborating types further down the dispatch pipeline.
/// </remarks>
public interface IWebhookEvent;