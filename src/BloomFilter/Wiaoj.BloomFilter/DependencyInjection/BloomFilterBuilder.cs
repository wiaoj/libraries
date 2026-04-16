using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.Hosting;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130
/// <summary>
/// A builder class used to configure Bloom Filter services via extension methods.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BloomFilterBuilder"/> class.
/// </remarks>
internal sealed class BloomFilterBuilder(IServiceCollection services) : IBloomFilterBuilder {
    /// <summary>
    /// Gets the service collection where services are registered.
    /// </summary>
    public IServiceCollection Services { get; } = services;
    public BloomFilterOptions Options { get; } = new(); 
    
    public IBloomFilterBuilder AddAutoSave() {
        services.AddHostedService<BloomFilterAutoSaveService>();
        return this;
    }

    public IBloomFilterBuilder AddWarmUp() {
        services.AddHostedService<BloomFilterWarmUpService>();
        return this;
    }

    public IBloomFilterBuilder AddAutoReseed() {
        services.AddHostedService<BloomFilterSeedingService>();
        return this;
    }
}