using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.DistributedCounter.DependencyInjection;
/// <summary>
/// A builder for configuring Wiaoj Distributed Counter services.
/// </summary>
public interface IDistributedCounterBuilder {
    /// <summary>
    /// Gets the application service collection.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets the configuration options.
    /// </summary>
    DistributedCounterOptions Options { get; }
}