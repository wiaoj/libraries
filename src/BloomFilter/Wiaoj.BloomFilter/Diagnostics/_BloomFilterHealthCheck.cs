//using System;
//using System.Collections.Generic;
//using System.Text;
//using Wiaoj.BloomFilter.Internal;

//namespace Wiaoj.BloomFilter.Diagnostics;

//public class BloomFilterHealthCheck(IBloomFilterRegistry registry) : IHealthCheck {
//    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) {
//        foreach(var filter in registry.GetAll()) {
//            // Eğer PopCount 0 ise ve filtre kirlenmiş (işlenmiş) ama yazamamışsa patlak demektir
//            if(filter.GetPopCount() == 0 && filter.IsDirty) {
//                return Task.FromResult(HealthCheckResult.Unhealthy($"Filter '{filter.Name}' is empty but dirty. Persistence failed?"));
//            }
//        }
//        return Task.FromResult(HealthCheckResult.Healthy());
//    }
//}

/*
 public static IServiceCollection AddBloomFilterHealthCheck(this IServiceCollection services) {
    services.AddHealthChecks().AddCheck<BloomFilterHealthCheck>("bloom_filter_health");
    return services;
}
 */