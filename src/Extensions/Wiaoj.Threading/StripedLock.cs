using Wiaoj.Concurrency.Extensions;

namespace Wiaoj.Concurrency;

/// <summary>
/// Provides a partitioned locking mechanism for a set of keys.
/// This reduces lock contention and memory usage compared to a single global lock or a lock per key.
/// </summary>
/// <typeparam name="TKey">The type of the key to lock on.</typeparam>
public class StripedLock<TKey> where TKey : notnull {
    private readonly AsyncLock[] _locks;

    /// <summary>
    /// Initializes a new instance of the StripedLock class.
    /// </summary>
    /// <param name="stripes">The number of locks to create. A higher number reduces contention but uses more memory. Must be a power of two for optimal performance.</param>
    public StripedLock(int stripes = 128) {
        // En iyi performans için sayının 2'nin kuvveti olduğundan emin olmak önemlidir.
        // Bu, modül (%) yerine bitwise AND (&) operasyonu kullanmamızı sağlar.
        Preca.ThrowIfNotPowerOfTwo(stripes);
        this._locks = new AsyncLock[stripes];
        for (int i = 0; i < stripes; i++) {
            this._locks[i] = new AsyncLock();
        }
    }

    /// <summary>
    /// Acquires a lock for the specified key.
    /// </summary>
    /// <param name="key">The key to lock on.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A disposable scope that releases the lock when disposed.</returns>
    public ValueTask<AsyncLock.Scope> LockAsync(TKey key, CancellationToken cancellationToken = default) {
        int stripeIndex = GetStripe(key);
        return this._locks[stripeIndex].LockAsync(cancellationToken);
    }

    private int GetStripe(TKey key) {
        // Eğer stripes sayısı 2'nin kuvveti ise (örneğin 128), modül (%) yerine
        // çok daha hızlı olan bitwise AND (&) operasyonunu kullanabiliriz.
        // 128 = 10000000 (binary), 127 = 01111111 (binary)
        return key.GetHashCode() & (this._locks.Length - 1);
    }
}