#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.RateLimiting.AspNetCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Specifies that rate limiting should be disabled for the targeted endpoint or controller.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class DisableRateLimitingAttribute : Attribute { }

/// <summary>
/// Specifies the cost consumed from the rate limit quota by the targeted endpoint.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RateLimitCostAttribute : Attribute {
    /// <summary>Gets the number of units consumed from the limit quota.</summary>
    public int Cost { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitCostAttribute"/> class.
    /// </summary>
    /// <param name="cost">The number of cost units consumed. Must be greater than zero.</param>
    public RateLimitCostAttribute(int cost) {
        if(cost <= 0) {
            throw new ArgumentOutOfRangeException(nameof(cost), "Rate limit cost must be greater than zero.");
        }
        this.Cost = cost;
    }
}