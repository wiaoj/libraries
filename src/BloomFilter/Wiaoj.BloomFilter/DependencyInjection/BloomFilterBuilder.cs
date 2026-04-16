using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wiaoj.BloomFilter.Internal;

namespace Wiaoj.BloomFilter.DependencyInjection;
/// <summary>
/// A builder class used to configure Bloom Filter services via extension methods.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BloomFilterBuilder"/> class.
/// </remarks>
public class BloomFilterBuilder(IServiceCollection services) {
    /// <summary>
    /// Gets the service collection where services are registered.
    /// </summary>
    public IServiceCollection Services { get; } = services;
}