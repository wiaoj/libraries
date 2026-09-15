using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Primitives.Cryptography.Symmetric;
using Wiaoj.Security.Testing;

namespace Wiaoj.Security.Tests.Unit.KeyRing;

/// <summary>
/// A subkey is HKDF-SHA256 of a versioned key for a named purpose: deterministic, separated by purpose, key and version,
/// and derived without exposing the key (#164).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "KeyRing")]
public class SubkeyDerivationTests {
    private static readonly byte[] Purpose = "wiaoj.identifiers"u8.ToArray();

    private static EncryptionKey Key(byte[] material, int version = 1, bool isRetired = false) {
        return new EncryptionKey(KeyVersion.Of(version), AesGcmKey.From(material), isRetired);
    }

    private static byte[] Derive(EncryptionKey key, byte[] purpose, int length = 32) {
        byte[] subkey = new byte[length];
        key.DeriveSubkey(purpose, subkey);
        return subkey;
    }

    [Fact]
    public void DeriveSubkey_ShouldMatchHkdfSha256WithThePrefixedPurpose() {
        byte[] material = RandomNumberGenerator.GetBytes(32);
        using EncryptionKey key = Key(material);

        byte[] expected = HKDF.DeriveKey(HashAlgorithmName.SHA256, material, 32, salt: null, info: [.. "wiaoj.security.subkey:"u8, .. Purpose]);

        Assert.Equal(expected, Derive(key, Purpose));
    }

    [Fact]
    public void DeriveSubkey_ShouldBeDeterministic() {
        byte[] material = RandomNumberGenerator.GetBytes(32);
        using EncryptionKey first = Key(material);
        using EncryptionKey second = Key(material);

        Assert.Equal(Derive(first, Purpose), Derive(second, Purpose));
    }

    [Fact]
    public void DeriveSubkey_ShouldGiveUnrelatedKeysForDifferentPurposesAndKeys() {
        byte[] material = RandomNumberGenerator.GetBytes(32);
        using EncryptionKey key = Key(material);
        using EncryptionKey other = Key(RandomNumberGenerator.GetBytes(32));

        byte[] identifiers = Derive(key, Purpose);

        Assert.NotEqual(identifiers, Derive(key, "wiaoj.other"u8.ToArray()));
        Assert.NotEqual(identifiers, Derive(other, Purpose));
        Assert.NotEqual(material, identifiers);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(255 * 32)]
    public void DeriveSubkey_ShouldFillADestinationOfAnyValidLength(int length) {
        using EncryptionKey key = Key(RandomNumberGenerator.GetBytes(32));

        byte[] subkey = Derive(key, Purpose, length);

        Assert.Equal(length, subkey.Length);
        if(length >= 16) {
            Assert.Contains(subkey, b => b != 0);
            // HKDF output is a prefix-stable stream: a shorter subkey is the start of a longer one.
            Assert.Equal(Derive(key, Purpose, 16), subkey[..16]);
        }
    }

    [Fact]
    public void DeriveSubkey_ShouldRefuseAnEmptyPurpose() {
        using EncryptionKey key = Key(RandomNumberGenerator.GetBytes(32));

        Assert.Throws<ArgumentException>(() => key.DeriveSubkey([], new byte[32]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255 * 32 + 1)]
    public void DeriveSubkey_ShouldRefuseAnInvalidLength(int length) {
        using EncryptionKey key = Key(RandomNumberGenerator.GetBytes(32));

        Assert.Throws<ArgumentOutOfRangeException>(() => key.DeriveSubkey(Purpose, new byte[length]));
    }

    [Fact]
    public void DeriveSubkey_ShouldWorkForARetiredKey() {
        using EncryptionKey retired = Key(RandomNumberGenerator.GetBytes(32), isRetired: true);

        Assert.Equal(32, Derive(retired, Purpose).Length);
    }

    [Fact]
    public void DeriveSubkey_ShouldRefuseADisposedKey() {
        EncryptionKey key = Key(RandomNumberGenerator.GetBytes(32));
        key.Dispose();

        Assert.Throws<ObjectDisposedException>(() => key.DeriveSubkey(Purpose, new byte[32]));
    }

    [Fact]
    public void KeyRing_ShouldListEveryVersionInOrder() {
        using KeyRing<WebhookTestContext> ring = new KeyRingBuilder<WebhookTestContext>()
            .WithRetiredKey(Key(RandomNumberGenerator.GetBytes(32), 3, isRetired: true))
            .WithCurrentKey(Key(RandomNumberGenerator.GetBytes(32), 5))
            .WithRetiredKey(Key(RandomNumberGenerator.GetBytes(32), 1, isRetired: true))
            .Build();

        Assert.Equal([KeyVersion.Of(1), KeyVersion.Of(3), KeyVersion.Of(5)], ring.Versions);
    }

    [Fact]
    public void SecretProtector_ShouldDeriveFromTheRequestedVersion() {
        byte[] v1 = RandomNumberGenerator.GetBytes(32);
        byte[] v2 = RandomNumberGenerator.GetBytes(32);
        using EncryptionKey expectedV1 = Key(v1);
        using EncryptionKey expectedV2 = Key(v2);

        using SecretProtector<WebhookTestContext> protector = new(new KeyRingBuilder<WebhookTestContext>()
            .WithRetiredKey(Key(v1, 1, isRetired: true))
            .WithCurrentKey(Key(v2, 2))
            .Build());
        ISubkeyDeriver<WebhookTestContext> deriver = protector;

        Assert.Equal(KeyVersion.Of(2), deriver.CurrentKeyVersion);
        Assert.Equal([KeyVersion.Of(1), KeyVersion.Of(2)], deriver.KeyVersions);

        byte[] fromV1 = new byte[32];
        byte[] fromV2 = new byte[32];
        deriver.DeriveSubkey(KeyVersion.Of(1), Purpose, fromV1);
        deriver.DeriveSubkey(KeyVersion.Of(2), Purpose, fromV2);

        Assert.Equal(Derive(expectedV1, Purpose), fromV1);
        Assert.Equal(Derive(expectedV2, Purpose), fromV2);
        Assert.Throws<KeyNotFoundException>(() => deriver.DeriveSubkey(KeyVersion.Of(9), Purpose, new byte[32]));
    }

    [Fact]
    public void SecretProtector_ShouldRefuseToDeriveOnceDisposed() {
        SecretProtector<WebhookTestContext> protector = new(new KeyRingBuilder<WebhookTestContext>()
            .WithCurrentKey(Key(RandomNumberGenerator.GetBytes(32)))
            .Build());
        protector.Dispose();

        Assert.Throws<ObjectDisposedException>(() => protector.DeriveSubkey(KeyVersion.Of(1), Purpose, new byte[32]));
        Assert.Throws<ObjectDisposedException>(() => protector.KeyVersions);
    }

    [Fact]
    public async Task AddManagedProtector_ShouldRegisterTheManagedProtectorAsTheSubkeyDeriver() {
        ServiceCollection services = new();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));
        services.AddWiaojSecurity().AddManagedProtector<WebhookTestContext>();
        services.Replace(ServiceDescriptor.Singleton<IMasterKeyProvider, FakeMasterKeyProvider>());
        services.Replace(ServiceDescriptor.Singleton<IEncryptionKeyStore, InMemoryEncryptionKeyStore>());

        await using ServiceProvider provider = services.BuildServiceProvider();
        ISubkeyDeriver<WebhookTestContext> deriver = provider.GetRequiredService<ISubkeyDeriver<WebhookTestContext>>();

        Assert.Same(provider.GetRequiredService<ManagedSecretProtector<WebhookTestContext>>(), deriver);
        Assert.Equal(KeyVersion.Of(1), deriver.CurrentKeyVersion);
        Assert.Equal([KeyVersion.Of(1)], deriver.KeyVersions);

        byte[] first = new byte[32];
        byte[] again = new byte[32];
        deriver.DeriveSubkey(KeyVersion.Of(1), Purpose, first);
        deriver.DeriveSubkey(KeyVersion.Of(1), Purpose, again);
        Assert.Equal(first, again);
    }

    [Fact]
    public async Task ManagedSecretProtector_ShouldExposeVersionsAddedByAReload() {
        ServiceCollection services = new();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>));
        services.AddWiaojSecurity().AddManagedProtector<WebhookTestContext>();
        services.Replace(ServiceDescriptor.Singleton<IMasterKeyProvider, FakeMasterKeyProvider>());
        services.Replace(ServiceDescriptor.Singleton<IEncryptionKeyStore, InMemoryEncryptionKeyStore>());

        await using ServiceProvider provider = services.BuildServiceProvider();
        ISubkeyDeriver<WebhookTestContext> deriver = provider.GetRequiredService<ISubkeyDeriver<WebhookTestContext>>();
        byte[] beforeRotation = new byte[32];
        deriver.DeriveSubkey(KeyVersion.Of(1), Purpose, beforeRotation);

        await using(AsyncServiceScope scope = provider.CreateAsyncScope()) {
            await scope.ServiceProvider.GetRequiredService<KeyRotationService<WebhookTestContext>>().ForceRotateAsync();
        }

        Assert.Equal(KeyVersion.Of(2), deriver.CurrentKeyVersion);
        Assert.Contains(KeyVersion.Of(1), deriver.KeyVersions);
        Assert.Contains(KeyVersion.Of(2), deriver.KeyVersions);

        byte[] afterRotation = new byte[32];
        deriver.DeriveSubkey(KeyVersion.Of(1), Purpose, afterRotation);
        Assert.Equal(beforeRotation, afterRotation);
    }
}
