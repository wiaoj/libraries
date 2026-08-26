using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Wiaoj.Concurrency;

/// <summary>
/// Delegate representing an atomic state mutation check for Compare-And-Swap operations.
/// </summary>
/// <typeparam name="TValue">The struct value type stored in the dictionary.</typeparam>
/// <typeparam name="TState">The readonly state payload passed to avoid closures.</typeparam>
/// <typeparam name="TResult">The operation decision or return value.</typeparam>
/// <param name="current">The current value if present, or <see langword="null"/> if the key does not exist yet.</param>
/// <param name="state">The contextual state payload.</param>
/// <returns>A tuple containing the next state to write, the operation result, and a boolean indicating whether to mutate.</returns>
public delegate (TValue Next, TResult Result, bool ShouldWrite) CasMutator<TValue, TState, TResult>(
    TValue? current,
    TState state) where TValue : struct;

/// <summary>
/// High-performance, lock-free Compare-And-Swap (CAS) extensions for <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
public static class ConcurrentDictionaryCasExtensions {
    /// <summary>
    /// Executes an atomic, lock-free optimistic concurrency mutation on a concurrent dictionary entry.
    /// Guarantees zero heap allocation when used with static lambdas.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult CompareAndSwap<TKey, TValue, TState, TResult>(
        this ConcurrentDictionary<TKey, TValue> store,
        TKey key,
        TState state,
        CasMutator<TValue, TState, TResult> mutator,
        TResult fallbackOnCancelled,
        CancellationToken cancellationToken = default)
        where TKey : notnull
        where TValue : struct {

        while(!cancellationToken.IsCancellationRequested) {
            // 1. Key yoksa: İlk ekleme kararı
            if(!store.TryGetValue(key, out TValue current)) {
                (TValue initial, TResult initialResult, bool shouldAdd) = mutator(null, state);
                if(!shouldAdd) {
                    return initialResult;
                }

                if(store.TryAdd(key, initial)) {
                    return initialResult;
                }

                continue; // Araya başka thread girdiyse döngü başa döner
            }

            // 2. Key varsa: Güncelleme kararı
            (TValue next, TResult result, bool shouldUpdate) = mutator(current, state);
            if(!shouldUpdate) {
                return result; // Limit aşıldıysa sözlüğe dokunmadan dön
            }

            // 3. Atomik CAS denemesi
            if(store.TryUpdate(key, next, current)) {
                return result;
            }
        }

        return fallbackOnCancelled;
    }
}