using Microsoft.Extensions.DependencyInjection;
using Wiaoj.DistributedCounter.Hosting;
using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter.DependencyInjection;

/// <summary>
/// Default internal builder implementation for configuring distributed counter components.
/// </summary>
internal sealed class DistributedCounterBuilder : IDistributedCounterBuilder {
    /// <inheritdoc/>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCounterBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public DistributedCounterBuilder(IServiceCollection services) {
        Preca.ThrowIfNull(services);
        this.Services = services;
    }

    /// <inheritdoc/>
    public IDistributedCounterBuilder Configure(Action<DistributedCounterOptions> configure) {
        Preca.ThrowIfNull(configure);
        this.Services.Configure(configure);
        return this;
    }

    /// <inheritdoc/>
    public IDistributedCounterBuilder AddAutoFlush() {
        this.Services.AddHostedService<CounterAutoFlushService>();
        return this;
    }
}