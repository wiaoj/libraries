namespace Wiaoj.Webhooks.LoopDetection;

/// <summary>
/// Defines the action executed by <see cref="LoopDetectionMiddleware"/> when an infinite delivery loop or hop limit breach is detected.
/// </summary>
public enum LoopDetectedBehavior {
    /// <summary>
    /// Short-circuits the pipeline, logs a warning, and marks the context with <see cref="WebhookDeliveryResult.LoopDetected"/>.
    /// </summary>
    DropAndLog = 0,

    /// <summary>
    /// Throws a <see cref="WebhookLoopDetectedException"/> to trigger failure escalation.
    /// </summary>
    ThrowException = 1
}
