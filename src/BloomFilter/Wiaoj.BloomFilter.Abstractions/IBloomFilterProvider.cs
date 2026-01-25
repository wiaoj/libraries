namespace Wiaoj.BloomFilter;
/// <summary>
/// Defines a provider for retrieving and managing the lifecycle of <see cref="IPersistentBloomFilter"/> instances.
/// This interface acts as the central entry point for accessing filters, handling lazy initialization and caching.
/// </summary>
public interface IBloomFilterProvider {
    /// <summary>
    /// Asynchronously retrieves an existing Bloom Filter or initializes a new one based on registered configurations.
    /// </summary>
    /// <remarks>
    /// This method uses an asynchronous lazy loading mechanism. If the filter is already in memory, it is returned immediately.
    /// If not, it attempts to load it from storage or creates a new instance.
    /// </remarks>
    /// <param name="name">The strongly-typed name of the filter to retrieve.</param>
    /// <returns>A task that represents the asynchronous operation. The value of the TResult parameter contains the requested <see cref="IPersistentBloomFilter"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the configuration for the specified name has not been registered.</exception>
    ValueTask<IPersistentBloomFilter> GetAsync(FilterName name);
}