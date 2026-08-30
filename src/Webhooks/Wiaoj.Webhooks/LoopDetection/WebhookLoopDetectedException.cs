namespace Wiaoj.Webhooks.LoopDetection;

/// <summary>
/// Exception thrown when an outbound or inbound webhook execution cycle or hop count threshold breach is detected.
/// </summary>
/// <param name="message">The message describing the detected loop condition.</param>
public sealed class WebhookLoopDetectedException(string message) : Exception(message);
