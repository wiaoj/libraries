using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.DistributedCounter;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A minimal builder contract for configuring distributed counter components, registrations, and storages.
/// </summary>
public interface IDistributedCounterBuilder {
    /// <summary>
    /// Gets the application service collection being configured.
    /// </summary>
    IServiceCollection Services { get; }
}