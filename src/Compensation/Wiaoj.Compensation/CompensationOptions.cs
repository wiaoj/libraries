using Wiaoj.Extensions;

namespace Wiaoj.Compensation;

/// <summary>
/// Configuration options for pipeline execution behavior.
/// </summary>
public sealed class CompensationOptions {
    /// <summary>
    /// Gets or sets the default timeout duration allocated strictly for the backward compensation phase.
    /// Default is 10 seconds.
    /// </summary>
    public TimeSpan DefaultRollbackTimeout { get; set; } = 10.Seconds();
}