using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.DistributedCounter.Internal.Memory;

#pragma warning disable IDE0130 
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130
public static class InMemoryDistributedCounterBuilderExtensions { 
    /// <summary>
    /// Configures the distributed counter to use In-Memory storage.
    /// Best for testing, development, or single-instance applications.
    /// NOT suitable for distributed environments (like Kubernetes with multiple replicas).
    /// </summary>
    public static IDistributedCounterBuilder UseInMemory(this IDistributedCounterBuilder builder) { 
        builder.Services.AddSingleton<ICounterStorage, InMemoryCounterStorage>();
        return builder;
    }
}