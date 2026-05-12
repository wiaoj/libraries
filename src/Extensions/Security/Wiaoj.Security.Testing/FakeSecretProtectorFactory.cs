using System.Collections.Concurrent;

namespace Wiaoj.Security.Testing;

/// <summary>
/// A factory that provides <see cref="FakeSecretProtector{TContext}"/> instances.
/// Useful for unit tests where multiple different contexts are used.
/// </summary>
public static class FakeSecretProtectorFactory {
    private static readonly ConcurrentDictionary<Type, object> _cache = new();

    /// <summary>
    /// Gets a singleton instance of <see cref="FakeSecretProtector{TContext}"/> for the given context.
    /// </summary>
    public static FakeSecretProtector<TContext> Get<TContext>() where TContext : ISecretContext {
        return (FakeSecretProtector<TContext>)_cache.GetOrAdd(typeof(TContext), _ => new FakeSecretProtector<TContext>());
    }

    /// <summary>
    /// Clears all cached fake protectors.
    /// </summary>
    public static void Reset() => _cache.Clear();
}
