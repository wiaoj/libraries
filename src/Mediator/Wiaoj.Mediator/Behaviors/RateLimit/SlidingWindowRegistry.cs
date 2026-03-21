using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Mediator.Behaviors;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>Per-type registry of sliding window counters. Thread-safe singleton.</summary>
internal static class SlidingWindowRegistry {
    private static readonly ConcurrentDictionary<Type, SlidingWindowCounter> _counters = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SlidingWindowCounter GetOrCreate(Type requestType, int maxRequests, TimeSpan window) {
        return _counters.GetOrAdd(requestType, _ => new SlidingWindowCounter(maxRequests, window));
    }
}