#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Mediator.Behaviors;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>Specifies the time window unit for <see cref="RateLimitAttribute"/>.</summary>
public enum RateLimitWindow {
    /// <summary>Per second.</summary>
    Second,
    /// <summary>Per minute.</summary>
    Minute,
    /// <summary>Per hour.</summary>
    Hour
}