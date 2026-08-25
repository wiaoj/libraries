namespace Wiaoj.RateLimiting;

/// <summary>
/// Root configuration options containing policy registrations and default policy settings.
/// </summary>
public sealed class RateLimitingOptions {
    /// <summary>
    /// Gets the registered policy factories indexed by policy name.
    /// </summary>
    public Dictionary<string, Func<IServiceProvider, IRateLimitAlgorithm>> Policies { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the fallback default policy factory delegate.
    /// </summary>
    public Func<IServiceProvider, IRateLimitAlgorithm>? DefaultPolicy { get; set; }
}