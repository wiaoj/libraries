using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.DistributedCounter.DependencyInjection;
internal sealed class DistributedCounterBuilder(IServiceCollection services, DistributedCounterOptions options)
    : IDistributedCounterBuilder {
    public IServiceCollection Services { get; } = services;
    public DistributedCounterOptions Options { get; } = options;
}