using Wiaoj.Concurrency;
using Wiaoj.Security.Testing;

namespace Wiaoj.Security.Tests.Unit.Protection;

[Trait("Category", "Unit")]
[Trait("Feature", "Protection")]
public class ManagedSecretProtectorTests {

    [Fact]
    public async Task EnsureInitializedAsync_ShouldPrewarmKeyRing() {
        // Arrange
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IMasterKeyProvider, FakeMasterKeyProvider>();
        services.AddSingleton<IEncryptionKeyStore, InMemoryEncryptionKeyStore>();
        services.AddOptions<KeyRotationOptions>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<KeyRingLoader<WebhookTestContext>>();

        IServiceProvider sp = services.BuildServiceProvider();
        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        AsyncLazy<SecretProtector<WebhookTestContext>> lazy = new(async ct => {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            KeyRingLoader<WebhookTestContext> loader = scope.ServiceProvider.GetRequiredService<KeyRingLoader<WebhookTestContext>>();
            KeyRing<WebhookTestContext> ring = await loader.LoadAsync(ct);
            return new SecretProtector<WebhookTestContext>(ring);
        });

        await using ManagedSecretProtector<WebhookTestContext> protector = new(lazy, scopeFactory);

        // Act
        Assert.False(protector.IsInitialized);
        await protector.EnsureInitializedAsync();

        // Assert
        Assert.True(protector.IsInitialized);
        Assert.Equal(1, protector.CurrentKeyVersion.Value);
    }
}