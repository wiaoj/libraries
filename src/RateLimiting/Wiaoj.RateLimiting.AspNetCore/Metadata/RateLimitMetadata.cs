using Microsoft.AspNetCore.Http;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.RateLimiting.AspNetCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents endpoint-specific rate limiting metadata such as static cost, dynamic cost resolver, or disabling policy.
/// </summary>
public sealed class RateLimitMetadata {
    /// <summary>Gets the static cost consumed by this endpoint, if defined.</summary>
    public int? Cost { get; init; }

    /// <summary>Gets a dynamic function to compute the cost from the current <see cref="HttpContext"/> (e.g. bulk batch count).</summary>
    public Func<HttpContext, int>? DynamicCostResolver { get; init; }

    /// <summary>Gets a value indicating whether rate limiting should be skipped entirely for this endpoint.</summary>
    public bool IsDisabled { get; init; }

    /// <summary>Gets an optional named rate limiting policy.</summary>
    public string? PolicyName { get; init; }
}