using Wiaoj.BloomFilter;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130
/// <summary>
/// A builder for configuring Wiaoj Bloom Filter services, options, and filter registrations.
/// </summary>
public interface IBloomFilterBuilder {
    /// <summary>
    /// Gets the <see cref="IServiceCollection"/> where services are configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets the configuration options instance being built.
    /// Internal usage allows extensions to modify options directly.
    /// </summary>
    BloomFilterOptions Options { get; }

    IBloomFilterBuilder AddAutoSave();
    IBloomFilterBuilder AddWarmUp();
    IBloomFilterBuilder AddAutoReseed();
}