using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.Hosting;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// A builder class used to configure Bloom Filter services via extension methods.
/// </summary>
internal sealed class BloomFilterBuilder(IServiceCollection services) : IBloomFilterBuilder {
    /// <inheritdoc/>
    public IServiceCollection Services { get; } = services;

    /// <inheritdoc/>
    public BloomFilterOptions Options { get; } = new(); 
    
    /// <inheritdoc/>
    public IBloomFilterBuilder AddAutoSave() {
        this.Services.AddHostedService<BloomFilterAutoSaveService>();
        return this;
    }

    /// <inheritdoc/>
    public IBloomFilterBuilder AddWarmUp() {
        this.Services.AddHostedService<BloomFilterWarmUpService>();
        return this;
    }

    /// <inheritdoc/>
    public IBloomFilterBuilder AddAutoReseed() {
        this.Services.AddHostedService<BloomFilterSeedingService>();
        return this;
    }
}