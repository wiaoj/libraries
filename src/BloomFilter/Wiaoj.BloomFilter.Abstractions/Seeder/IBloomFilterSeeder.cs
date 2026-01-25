namespace Wiaoj.BloomFilter.Seeder;
/// <summary>
/// Defines a contract for seeding (populating) a Bloom Filter from an external data source.
/// Useful for initial hydration or warming up the filter from a database or API stream.
/// </summary>
public interface IBloomFilterSeeder {
    /// <summary>
    /// Asynchronously streams string data from a source and adds it to the specified filter.
    /// </summary>
    /// <param name="filterName">The unique name of the filter to seed.</param>
    /// <param name="source">An async enumerable source of string keys.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the completion of the seed operation.</returns>
    Task SeedAsync(
        FilterName filterName,
        IAsyncEnumerable<string> source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously streams generic data from a source, converts it to bytes, and adds it to the specified filter.
    /// </summary>
    /// <typeparam name="T">The type of items in the source.</typeparam>
    /// <param name="filterName">The unique name of the filter to seed.</param>
    /// <param name="source">An async enumerable source of items.</param>
    /// <param name="serializer">A delegate to convert the generic item into a read-only byte span.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the completion of the seed operation.</returns>
    Task SeedAsync<T>(
        FilterName filterName,
        IAsyncEnumerable<T> source,
        Func<T, ReadOnlySpan<byte>> serializer,
        CancellationToken cancellationToken = default);

    // --- Typed Overloads ---

    /// <summary>
    /// Asynchronously seeds the Bloom Filter associated with the specified tag type <typeparamref name="TTag"/> 
    /// using a stream of string items.
    /// </summary>
    /// <remarks>
    /// This method resolves the filter name registered for <typeparamref name="TTag"/> via Dependency Injection
    /// and streams data from the <paramref name="source"/> into the filter without loading the entire dataset into memory.
    /// </remarks>
    /// <typeparam name="TTag">The marker type used to identify the specific Bloom Filter configuration (e.g., <c>UserTag</c>).</typeparam>
    /// <param name="source">An asynchronous stream of string items to be added to the filter.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the completion of the seeding and persistence operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no filter is registered for the specified <typeparamref name="TTag"/>.</exception>
    Task SeedAsync<TTag>(
        IAsyncEnumerable<string> source,
        CancellationToken cancellationToken = default)
        where TTag : notnull;

    /// <summary>
    /// Asynchronously seeds the Bloom Filter associated with the specified tag type <typeparamref name="TTag"/> 
    /// using a stream of generic items converted to bytes.
    /// </summary>
    /// <remarks>
    /// This method resolves the filter name registered for <typeparamref name="TTag"/> via Dependency Injection.
    /// It uses the provided <paramref name="serializer"/> to convert each item into a byte span efficiently,
    /// allowing for zero-allocation seeding strategies.
    /// </remarks>
    /// <typeparam name="TTag">The marker type used to identify the specific Bloom Filter configuration (e.g., <c>ProductTag</c>).</typeparam>
    /// <typeparam name="TItem">The type of the items in the source stream (e.g., <c>Guid</c>, <c>int</c>, or a DTO).</typeparam>
    /// <param name="source">An asynchronous stream of items to be added to the filter.</param>
    /// <param name="serializer">A delegate that converts a <typeparamref name="TItem"/> into a <see cref="ReadOnlySpan{Byte}"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the completion of the seeding and persistence operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no filter is registered for the specified <typeparamref name="TTag"/>.</exception>
    Task SeedAsync<TTag, TItem>(
        IAsyncEnumerable<TItem> source,
        Func<TItem, ReadOnlySpan<byte>> serializer,
        CancellationToken cancellationToken = default)
        where TTag : notnull;
}