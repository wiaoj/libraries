using System.Diagnostics;

namespace Wiaoj.DistributedCounter.Diagnostics;
internal static class DistributedCounterTracing {  
    public static readonly ActivitySource Source = new("Wiaoj.DistributedCounter", "1.0.0");
}