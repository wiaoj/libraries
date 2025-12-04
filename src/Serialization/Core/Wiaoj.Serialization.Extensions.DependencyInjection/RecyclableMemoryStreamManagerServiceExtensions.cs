using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IO;

namespace Wiaoj.Serialization.Extensions.DependencyInjection;
/// <summary>
/// Provides extension methods for registering the RecyclableMemoryStreamManager.
/// </summary>
public static class RecyclableMemoryStreamManagerServiceExtensions {
    /// <summary>
    /// Registers a singleton instance of <see cref="RecyclableMemoryStreamManager"/> in the service collection.
    /// This is recommended for performance-critical applications to reduce memory allocations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddRecyclableMemoryStreamManager(this IServiceCollection services) {
        services.TryAddSingleton<RecyclableMemoryStreamManager>();
        return services;
    }
}