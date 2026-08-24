using System.Runtime.CompilerServices;

namespace Wiaoj.RateLimiting.Internal;

/// <summary>
/// Internal mathematical helper for Generic Cell Rate Algorithm (GCRA) and queue capacity calculations.
/// </summary>
internal static class GcraMath {
    /// <summary>
    /// Computes the remaining capacity (headroom) based on the theoretical arrival time (TAT) and current time.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ComputeRemaining(DateTimeOffset tat, DateTimeOffset now, long burstToleranceTicks, long emissionIntervalTicks) {
        if(emissionIntervalTicks <= 0 || burstToleranceTicks <= 0) {
            return 0;
        }

        long debtTicks = Math.Max(0, (tat - now).Ticks);
        return Math.Max(0, (burstToleranceTicks - debtTicks) / emissionIntervalTicks);
    }
}