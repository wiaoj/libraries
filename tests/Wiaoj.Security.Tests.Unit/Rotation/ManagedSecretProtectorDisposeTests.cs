using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Security.Testing;

namespace Wiaoj.Security.Tests.Unit.Rotation;

/// <summary>
/// Synchronous <c>Dispose()</c> began disposal and then called <c>DisposeAsync()</c>, which saw disposal already begun
/// and waited for it to finish — which only happened after it returned. Every synchronous dispose, including a
/// <c>using ServiceProvider</c>, hung forever.
/// </summary>
public sealed class ManagedSecretProtectorDisposeTests {
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiceProvider Build() {
        ServiceCollection services = new();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddWiaojSecurity().AddManagedProtector<WebhookTestContext>();
        services.Replace(ServiceDescriptor.Singleton<IMasterKeyProvider, FakeMasterKeyProvider>());
        services.Replace(ServiceDescriptor.Singleton<IEncryptionKeyStore, InMemoryEncryptionKeyStore>());
        return services.BuildServiceProvider();
    }

    private static ISecretProtector<WebhookTestContext> ProtectorWithLoadedKeys(IServiceProvider provider) {
        ISecretProtector<WebhookTestContext> protector = provider.GetRequiredService<ISecretProtector<WebhookTestContext>>();
        protector.Protect("load the key ring");
        return protector;
    }

    [Fact]
    public async Task Dispose_Returns_After_The_Keys_Were_Loaded() {
        ServiceProvider provider = Build();
        ProtectorWithLoadedKeys(provider);

        await Task.Run(provider.Dispose, Ct).WaitAsync(Deadline, Ct);
    }

    [Fact]
    public async Task Dispose_Returns_When_The_Keys_Were_Never_Loaded() {
        ServiceProvider provider = Build();
        _ = provider.GetRequiredService<ISecretProtector<WebhookTestContext>>();

        await Task.Run(provider.Dispose, Ct).WaitAsync(Deadline, Ct);
    }

    [Fact]
    public async Task Dispose_And_DisposeAsync_Can_Follow_Each_Other_In_Either_Order() {
        ServiceProvider provider = Build();
        var protector = (ManagedSecretProtector<WebhookTestContext>)ProtectorWithLoadedKeys(provider);

        await Task.Run(protector.Dispose, Ct).WaitAsync(Deadline, Ct);
        await protector.DisposeAsync().AsTask().WaitAsync(Deadline, Ct);
        await Task.Run(protector.Dispose, Ct).WaitAsync(Deadline, Ct);
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task A_Disposed_Protector_Refuses_Work() {
        ServiceProvider provider = Build();
        ISecretProtector<WebhookTestContext> protector = ProtectorWithLoadedKeys(provider);

        await Task.Run(provider.Dispose, Ct).WaitAsync(Deadline, Ct);

        Assert.Throws<ObjectDisposedException>(() => protector.Protect("after dispose"));
    }
}
