using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Serialization.DependencyInjection;

public interface IServiceCollectionAccessor {
    IServiceCollection Services { get; }
}