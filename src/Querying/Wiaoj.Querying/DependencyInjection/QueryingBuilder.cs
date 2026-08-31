using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Querying.DependencyInjection;

/// <summary>
/// Internal default implementation of <see cref="IQueryingBuilder"/>.
/// </summary>
internal sealed class QueryingBuilder(IServiceCollection services) : IQueryingBuilder {
    public IServiceCollection Services { get; } = services;
}