using System.Diagnostics.Metrics;

namespace Wiaoj.DistributedCounter.Internal;
internal interface IBufferedCounterSource {
    IEnumerable<BufferedDistributedCounter> GetBufferedCounters();

    IEnumerable<IDistributedCounter> GetAllTrackedCounters();

    void ClearCache();
}