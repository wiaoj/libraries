using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Querying;

/// <summary>
/// A minimal builder contract for configuring query engine components, schemas, and payload parsers.
/// </summary>
public interface IQueryingBuilder {
    /// <summary>
    /// Gets the application service collection being configured.
    /// </summary>
    IServiceCollection Services { get; }
}