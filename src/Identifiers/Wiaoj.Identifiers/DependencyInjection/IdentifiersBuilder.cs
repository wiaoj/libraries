using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Preconditions;

namespace Wiaoj.Identifiers.DependencyInjection;

internal sealed class IdentifiersBuilder(IServiceCollection services) : IIdentifiersBuilder {
    /// <inheritdoc/>
    public IServiceCollection Services { get; } = services;

    /// <inheritdoc/>
    public IIdentifiersBuilder UseCodec(Func<IServiceProvider, IdCodec> factory) {
        Preca.ThrowIfNull(factory);

        this.Services.Replace(ServiceDescriptor.Singleton(factory));
        return this;
    }
}
