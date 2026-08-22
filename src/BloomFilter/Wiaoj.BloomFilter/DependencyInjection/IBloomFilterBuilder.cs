using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.BloomFilter;
#pragma warning restore IDE0130 // Namespace does not match folder structure
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

    /// <summary>
    /// Enables automatic periodic saving of dirty Bloom Filters to the configured storage.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    IBloomFilterBuilder AddAutoSave();

    /// <summary>
    /// Enables background warming up of all registered filters during application startup.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    IBloomFilterBuilder AddWarmUp();

    /// <summary>
    /// Enables automatic background reseeding of filters when they are found to be empty or corrupted.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    IBloomFilterBuilder AddAutoReseed();
}