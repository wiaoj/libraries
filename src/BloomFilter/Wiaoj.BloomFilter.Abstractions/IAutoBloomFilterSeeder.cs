namespace Wiaoj.BloomFilter;
/// <summary>
/// Implement this interface to enable automatic reseeding when a filter is corrupted or reset.
/// </summary>
public interface IAutoBloomFilterSeeder {
    /// <summary>
    /// The name of the filter this seeder is responsible for.
    /// </summary>
    FilterName FilterName { get; }

    /// <summary>
    /// The logic to refill the filter from the source (DB, API, etc.).
    /// </summary>
    Task SeedAsync(IPersistentBloomFilter filter, CancellationToken cancellationToken);
}