using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.DistributedCounter.DependencyInjection;

/// <summary>
/// A fluent builder for configuring Wiaoj Distributed Counter services, storages, and background workers.
/// </summary>
public interface IDistributedCounterBuilder {
    /// <summary>
    /// Gets the application service collection being configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Configures global options for the distributed counter engine.
    /// </summary>
    /// <param name="configure">The delegate used to configure <see cref="DistributedCounterOptions"/>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IDistributedCounterBuilder Configure(Action<DistributedCounterOptions> configure);

    /// <summary>
    /// Enables the background periodic auto-flush service for buffered counters.
    /// </summary>
    /// <returns>The builder instance for fluent chaining.</returns>
    IDistributedCounterBuilder AddAutoFlush();
}