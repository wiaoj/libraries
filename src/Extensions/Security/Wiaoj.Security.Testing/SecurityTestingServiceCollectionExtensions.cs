using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wiaoj.Security.Testing;

public static class SecurityTestingServiceCollectionExtensions {
    
    /// <summary>
    /// Registers a <see cref="FakeSecretProtector{TContext}"/> for the specified context.
    /// Useful for integration tests where you want to swap the real protector with a fake one.
    /// </summary>
    public static IServiceCollection AddFakeSecretProtector<TContext>(this IServiceCollection services)
        where TContext : ISecretContext {
        
        services.Replace(ServiceDescriptor.Singleton<ISecretProtector<TContext>, FakeSecretProtector<TContext>>());
        return services;
    }

    /// <summary>
    /// Registers a single instance of <see cref="FakeSecretProtector{TContext}"/> that can be resolved
    /// to multiple contexts if they are implemented by the same class.
    /// </summary>
    public static IServiceCollection AddFakeSecretProtector<TContext, TImplementation>(this IServiceCollection services)
        where TContext : ISecretContext
        where TImplementation : class, ISecretProtector<TContext> {
        
        services.Replace(ServiceDescriptor.Singleton<ISecretProtector<TContext>, TImplementation>());
        return services;
    }

    /// <summary>
    /// Registers <see cref="FakeMasterKeyProvider"/> and <see cref="InMemoryEncryptionKeyStore"/>.
    /// Use this if you want to test the REAL SecretProtector and KeyRingLoader 
    /// but without real HSM/Vault or Database dependencies.
    /// </summary>
    public static IServiceCollection AddFakeSecurityInfrastructure(this IServiceCollection services) {
        services.Replace(ServiceDescriptor.Singleton<IMasterKeyProvider, FakeMasterKeyProvider>());
        services.Replace(ServiceDescriptor.Singleton<IEncryptionKeyStore, InMemoryEncryptionKeyStore>());
        return services;
    }
}
