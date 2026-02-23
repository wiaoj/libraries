namespace Wiaoj.DistributedCounter;
/// <summary>
/// Provides factory methods for creating or retrieving <see cref="IDistributedCounter"/> instances.
/// Supports named counters, strongly-typed markers, and dynamic keys.
/// </summary>
public interface IDistributedCounterFactory {

    /// <summary>
    /// Creates or retrieves a distributed counter using a simple name.
    /// </summary>
    /// <param name="name">The unique name of the counter.</param>
    /// <returns>A counter instance associated with the specified name.</returns>
    IDistributedCounter Create(string name);

    /// <summary>
    /// Creates or retrieves a strongly-typed distributed counter using a marker type.
    /// Useful for Dependency Injection and grouping related counters.
    /// </summary>
    /// <typeparam name="TTag">The marker type (class or struct) representing the counter category.</typeparam>
    /// <returns>A counter instance associated with the specified type.</returns>
    IDistributedCounter Create<TTag>() where TTag : notnull;

    /// <summary>
    /// Creates or retrieves a named counter that is scoped to a specific identity key.
    /// Example: Create("UserLoginAttempts", userId).
    /// </summary>
    /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
    /// <param name="name">The base name/category of the counter.</param>
    /// <param name="key">The specific identity key (e.g., User ID, IP Address).</param>
    /// <returns>A counter instance scoped to the name and specific key.</returns>
    IDistributedCounter Create<TKey>(string name, TKey key) where TKey : notnull;

    /// <summary>
    /// Creates or retrieves a strongly-typed counter scoped to a specific identity key.
    /// This is the most robust way to define counters, providing both type safety and dynamic scoping.
    /// Example: Create&lt;RateLimit, string&gt;(ipAddress).
    /// </summary>
    /// <typeparam name="TTag">The marker type representing the counter category.</typeparam>
    /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
    /// <param name="key">The specific identity key.</param>
    /// <returns>A counter instance scoped to the tag and specific key.</returns>
    IDistributedCounter Create<TTag, TKey>(TKey key) where TTag : notnull where TKey : notnull; 
}