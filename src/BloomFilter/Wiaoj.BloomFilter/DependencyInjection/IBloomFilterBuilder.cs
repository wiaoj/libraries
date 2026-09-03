using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.BloomFilter;

/// <summary>
/// Builder interface for configuring Bloom Filter infrastructure and registering typed filters.
/// </summary>
public interface IBloomFilterBuilder {
    /// <summary>
    /// Gets the application service collection.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets the Bloom Filter configuration options.
    /// </summary>
    BloomFilterOptions Options { get; }

    /// <summary>
    /// Enables periodic automatic saving of dirty filters to storage.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    IBloomFilterBuilder AddAutoSave();

    /// <summary>
    /// Enables application startup preloading of all configured filters.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    IBloomFilterBuilder AddWarmUp();

    /// <summary>
    /// Enables background automatic reseeding when filters are empty or corrupted.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    IBloomFilterBuilder AddAutoReseed();
}