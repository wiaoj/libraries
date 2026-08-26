using System.Runtime.CompilerServices;
using Wiaoj.Primitives;

namespace Wiaoj.RateLimiting.Internal;

/// <summary>
/// Mathematical calculation helper for the Generic Cell Rate Algorithm (GCRA) and virtual scheduling traffic shaping.
/// Provides zero-allocation formulas for determining remaining burst headroom from Theoretical Arrival Time (TAT).
/// </summary>
/// <remarks>
/// GCRA models a leaky bucket as a virtual schedule. Remaining burst capacity is computed as:
/// <c>Remaining = max(0, (BurstTolerance - max(0, TAT - Now)) / EmissionInterval)</c>.
/// </remarks>
internal static class GcraMath {
    /// <summary>
    /// Computes the remaining token/cell capacity headroom using monotonic timestamps.
    /// </summary>
    /// <param name="tat">The Theoretical Arrival Time (TAT) of the tracked resource.</param>
    /// <param name="now">The current monotonic timestamp.</param>
    /// <param name="burstToleranceTicks">The maximum burst tolerance duration represented in ticks.</param>
    /// <param name="emissionIntervalTicks">The emission interval duration per unit cost represented in ticks.</param>
    /// <returns>The number of remaining cost units that can be admitted immediately before violating burst tolerance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ComputeRemaining(
        MonotonicTimestamp tat,
        MonotonicTimestamp now,
        long burstToleranceTicks,
        long emissionIntervalTicks) {

        if(emissionIntervalTicks <= 0 || burstToleranceTicks <= 0) {
            return 0;
        }

        long debtTicks = Math.Max(0, (tat - now).Ticks);
        return Math.Max(0, (burstToleranceTicks - debtTicks) / emissionIntervalTicks);
    }

    /// <summary>
    /// Computes the remaining token/cell capacity headroom using wall-clock date timestamps.
    /// </summary>
    /// <param name="tat">The Theoretical Arrival Time (TAT) of the tracked resource.</param>
    /// <param name="now">The current wall-clock date timestamp.</param>
    /// <param name="burstToleranceTicks">The maximum burst tolerance duration represented in ticks.</param>
    /// <param name="emissionIntervalTicks">The emission interval duration per unit cost represented in ticks.</param>
    /// <returns>The number of remaining cost units that can be admitted immediately before violating burst tolerance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ComputeRemaining(
        DateTimeOffset tat,
        DateTimeOffset now,
        long burstToleranceTicks,
        long emissionIntervalTicks) {

        if(emissionIntervalTicks <= 0 || burstToleranceTicks <= 0) {
            return 0;
        }

        long debtTicks = Math.Max(0, (tat - now).Ticks);
        return Math.Max(0, (burstToleranceTicks - debtTicks) / emissionIntervalTicks);
    }
}