using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using Wiaoj.Concurrency;
using Wiaoj.Security.Testing;

namespace Wiaoj.Security.Tests.Unit.KeyWrapping;

[Trait("Category", "Unit")]
[Trait("Feature", "KeyWrapping")]
public class MasterKeyRewrapServiceTests {

    private sealed class TestPreviousMasterKeyProvider(MasterKey? key) : IPreviousMasterKeyProvider {
        public ValueTask<MasterKey?> GetPreviousMasterKeyAsync(CancellationToken ct = default) {
            return ValueTask.FromResult(key);
        }
    }

    private sealed class TestCurrentMasterKeyProvider(MasterKey key) : IMasterKeyProvider {
        public ValueTask<MasterKey> GetMasterKeyAsync(CancellationToken ct = default) {
            return ValueTask.FromResult(new MasterKey(Secret.From(key.Expose(s => s.ToArray()))));
        }
    }

    private static (IServiceScopeFactory ScopeFactory, ManagedSecretProtector<WebhookTestContext> Protector) CreateProtector(
        IEncryptionKeyStore store,
        IMasterKeyProvider masterProvider) {

        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(store);
        services.AddSingleton(masterProvider);
        services.AddOptions<KeyRotationOptions>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<KeyRingLoader<WebhookTestContext>>();

        IServiceProvider sp = services.BuildServiceProvider();
        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        AsyncLazy<SecretProtector<WebhookTestContext>> lazy = new(async ct => {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            KeyRingLoader<WebhookTestContext> loader = scope.ServiceProvider.GetRequiredService<KeyRingLoader<WebhookTestContext>>();
            return new SecretProtector<WebhookTestContext>(await loader.LoadAsync(ct));
        });

        return (scopeFactory, new ManagedSecretProtector<WebhookTestContext>(lazy, scopeFactory));
    }

    [Fact]
    public async Task RewrapAllAsync_WhenPreviousProviderNotRegistered_ShouldThrowInvalidOperationException() {
        // Arrange
        InMemoryEncryptionKeyStore store = new();
        using MasterKey currentMaster = new(Secret.Generate(32));
        TestCurrentMasterKeyProvider masterProvider = new(currentMaster);

        var (_, protector) = CreateProtector(store, masterProvider);
        await using(protector) {
            MasterKeyRewrapService<WebhookTestContext> service = new(
                store,
                masterProvider,
                protector,
                NullLogger<MasterKeyRewrapService<WebhookTestContext>>.Instance,
                previousMaster: null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RewrapAllAsync());
        }
    }

    [Fact]
    public async Task RewrapAllAsync_ShouldRewrapLegacyKeysAndBeIdempotentOnSecondRun() {
        // Arrange
        InMemoryEncryptionKeyStore store = new();

        using MasterKey oldMaster = new(Secret.Generate(32));
        using MasterKey newMaster = new(Secret.Generate(32));

        byte[] dek1 = RandomNumberGenerator.GetBytes(32);
        byte[] dek2 = RandomNumberGenerator.GetBytes(32);

        await store.SaveKeyAsync(new EncryptionKeyRecord {
            Id = Guid.CreateVersion7(),
            ContextName = nameof(WebhookTestContext),
            Version = 1,
            WrappedKeyMaterial = oldMaster.Wrap(dek1),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-100),
            RetiredAt = DateTimeOffset.UtcNow.AddDays(-10)
        });

        await store.SaveKeyAsync(new EncryptionKeyRecord {
            Id = Guid.CreateVersion7(),
            ContextName = nameof(WebhookTestContext),
            Version = 2,
            WrappedKeyMaterial = oldMaster.Wrap(dek2),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
        });

        TestCurrentMasterKeyProvider currentProvider = new(newMaster);
        TestPreviousMasterKeyProvider previousProvider = new(oldMaster);

        var (_, protector) = CreateProtector(store, currentProvider);
        await using(protector) {
            MasterKeyRewrapService<WebhookTestContext> service = new(
                store,
                currentProvider,
                protector,
                NullLogger<MasterKeyRewrapService<WebhookTestContext>>.Instance,
                previousProvider);

            // Act 1: Initial rewrap
            MasterKeyRewrapResult result1 = await service.RewrapAllAsync();

            // Assert 1
            Assert.Equal(2, result1.Total);
            Assert.Equal(2, result1.Rewrapped);
            Assert.Equal(0, result1.AlreadyCurrent);
            Assert.Equal(0, result1.Failed);
            Assert.True(result1.IsComplete);

            // Act 2: Idempotent second run
            MasterKeyRewrapResult result2 = await service.RewrapAllAsync();

            // Assert 2
            Assert.Equal(2, result2.Total);
            Assert.Equal(0, result2.Rewrapped);
            Assert.Equal(2, result2.AlreadyCurrent);
            Assert.Equal(0, result2.Failed);
            Assert.True(result2.IsComplete);
        }
    }

    [Fact]
    public async Task RewrapAllAsync_WhenKeyCorrupted_ShouldRecordFailedAndContinue() {
        // Arrange
        InMemoryEncryptionKeyStore store = new();
        using MasterKey oldMaster = new(Secret.Generate(32));
        using MasterKey newMaster = new(Secret.Generate(32));

        // Corrupted payload that cannot be unwrapped
        await store.SaveKeyAsync(new EncryptionKeyRecord {
            Id = Guid.CreateVersion7(),
            ContextName = nameof(WebhookTestContext),
            Version = 1,
            WrappedKeyMaterial = "totally-corrupted-base64-payload",
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Valid key that can be unwrapped to allow protector.ReloadAsync to succeed
        byte[] dek2 = RandomNumberGenerator.GetBytes(32);
        await store.SaveKeyAsync(new EncryptionKeyRecord {
            Id = Guid.CreateVersion7(),
            ContextName = nameof(WebhookTestContext),
            Version = 2,
            WrappedKeyMaterial = oldMaster.Wrap(dek2),
            CreatedAt = DateTimeOffset.UtcNow
        });

        TestCurrentMasterKeyProvider currentProvider = new(newMaster);
        TestPreviousMasterKeyProvider previousProvider = new(oldMaster);

        var (_, protector) = CreateProtector(store, currentProvider);
        await using(protector) {
            MasterKeyRewrapService<WebhookTestContext> service = new(
                store,
                currentProvider,
                protector,
                NullLogger<MasterKeyRewrapService<WebhookTestContext>>.Instance,
                previousProvider);

            // Act
            MasterKeyRewrapResult result = await service.RewrapAllAsync();

            // Assert
            Assert.Equal(2, result.Total);
            Assert.Equal(1, result.Rewrapped);
            Assert.Equal(1, result.Failed);
            Assert.False(result.IsComplete);
        }
    }
}