using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wiaoj.Security;
using Wiaoj.Security.Testing;

namespace Wiaoj.Identifiers.Security.Tests.Unit;

[CollectionDefinition(nameof(InstalledCodecCollection), DisableParallelization = true)]
public sealed class InstalledCodecCollection;

/// <summary>
/// With the real managed protector, UseKeyRingCodec installs a codec that follows key rotation: identifiers written
/// after a rotation carry the new version, and earlier ones still parse (#165).
/// </summary>
[Collection(nameof(InstalledCodecCollection))]
[Trait("Category", "Integration")]
[Trait("Feature", "Identifiers")]
[Trait("Component", "KeyRingRotation")]
public sealed class KeyRingRotationTests : IDisposable {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public KeyRingRotationTests() => IdCodec.ResetInstalled();

    public void Dispose() => IdCodec.ResetInstalled();

    private static IHost Build() {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Logging.ClearProviders();
        builder.Services.AddWiaojSecurity().AddManagedProtector<IdentifierContext>();
        builder.Services.Replace(ServiceDescriptor.Singleton<IMasterKeyProvider, FakeMasterKeyProvider>());
        builder.Services.Replace(ServiceDescriptor.Singleton<IEncryptionKeyStore, InMemoryEncryptionKeyStore>());
        builder.Services.AddIdentifiers().UseKeyRingCodec<IdentifierContext>();
        return builder.Build();
    }

    [Fact]
    public async Task Should_Install_A_Codec_That_Follows_Key_Rotation() {
        using IHost host = Build();
        await host.StartAsync(Ct);

        Assert.IsType<KeyRingIdCodec<IdentifierContext>>(IdCodec.Current);

        InvoiceId id = InvoiceId.New();
        string beforeRotation = id.ToString();
        Assert.StartsWith("inv_1", beforeRotation, StringComparison.Ordinal);

        await using(AsyncServiceScope scope = host.Services.CreateAsyncScope()) {
            await scope.ServiceProvider.GetRequiredService<KeyRotationService<IdentifierContext>>().ForceRotateAsync(Ct);
        }

        string afterRotation = id.ToString();
        Assert.StartsWith("inv_2", afterRotation, StringComparison.Ordinal);
        Assert.Equal(id, InvoiceId.Parse(beforeRotation));
        Assert.Equal(id, InvoiceId.Parse(afterRotation));

        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task Should_Refuse_To_Start_Without_A_Managed_Protector_For_The_Context() {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Logging.ClearProviders();
        builder.Services.AddIdentifiers().UseKeyRingCodec<IdentifierContext>();
        using IHost host = builder.Build();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(Ct));

        Assert.Contains(nameof(ISubkeyDeriver<IdentifierContext>), error.Message, StringComparison.Ordinal);
    }
}
