using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wiaoj.BloomFilter.Internal;

namespace Wiaoj.BloomFilter.DependencyInjection;
/// <summary>
/// A builder class used to configure Bloom Filter services via extension methods.
/// </summary>
public class BloomFilterBuilder {
    /// <summary>
    /// Gets the service collection where services are registered.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BloomFilterBuilder"/> class.
    /// </summary>
    public BloomFilterBuilder(IServiceCollection services) {
        Services = services;
    }
}