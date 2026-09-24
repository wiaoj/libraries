using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wiaoj.Identifiers.Tests.Unit;

/// <summary>
/// AddIdentifiers installs the chosen codec when the host starts, and refuses to start without a codec or with an
/// unusable key.
/// </summary>
[Collection(nameof(InstalledCodecCollection))]
[Trait("Category", "Unit")]
[Trait("Feature", "Identifiers")]
[Trait("Component", "Registration")]
public sealed class IdentifiersRegistrationTests : IDisposable {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string Base64Key = Convert.ToBase64String(TestCodecs.Key);

    public IdentifiersRegistrationTests() => IdCodec.ResetInstalled();

    public void Dispose() => IdCodec.ResetInstalled();

    private static IHost Build(Action<IServiceCollection> configure) {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Logging.ClearProviders();
        configure(builder.Services);
        return builder.Build();
    }

    [Fact]
    public async Task Should_Install_The_Plain_Codec_When_The_Host_Starts() {
        using IHost host = Build(s => s.AddIdentifiers().UsePlainCodec());

        Assert.Throws<InvalidOperationException>(() => IdCodec.Current);
        await host.StartAsync(Ct);

        Assert.Same(PlainIdCodec.Instance, IdCodec.Current);
        Assert.Equal("usr_z", UserId.From(new(61)).ToString());
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task Should_Install_The_Aes_Codec_With_The_Configured_Key() {
        using IHost host = Build(s => s.AddIdentifiers().UseAesCodec(o => {
            o.AesKey = Base64Key;
            o.AesKeyVersion = 'k';
        }));

        await host.StartAsync(Ct);

        AesIdCodec codec = Assert.IsType<AesIdCodec>(IdCodec.Current);
        Assert.Equal('k', codec.Version);
        Assert.Equal(TestCodecs.Aes('k').Encode("usr", new(42)), UserId.From(new(42)).ToString());
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task Should_Bind_The_Key_From_Configuration() {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Identifiers:AesKey"] = Base64Key });
        builder.Services.AddIdentifiers().UseAesCodec();
        builder.Services.Configure<IdentifiersOptions>(builder.Configuration.GetSection("Identifiers"));
        using IHost host = builder.Build();

        await host.StartAsync(Ct);

        Assert.Equal(TestCodecs.Aes().Encode("usr", new(42)), UserId.From(new(42)).ToString());
        await host.StopAsync(Ct);
    }

    [Theory]
    [InlineData(null, "is not set")]
    [InlineData("   ", "is not set")]
    [InlineData("not base64!", "not valid base64")]
    [InlineData("AAECAwQFBgcICQoLDA0O", "15 bytes")]
    public async Task Should_Refuse_To_Start_Without_A_Usable_Key(string? key, string message) {
        using IHost host = Build(s => s.AddIdentifiers().UseAesCodec(o => o.AesKey = key));

        OptionsValidationException error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(Ct));

        Assert.Contains(message, error.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => IdCodec.Current);
    }

    [Fact]
    public async Task Should_Refuse_To_Start_With_An_Invalid_Version() {
        using IHost host = Build(s => s.AddIdentifiers().UseAesCodec(o => {
            o.AesKey = Base64Key;
            o.AesKeyVersion = '_';
        }));

        OptionsValidationException error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(Ct));
        Assert.Contains("AesKeyVersion", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_Refuse_To_Start_When_No_Codec_Was_Chosen() {
        using IHost host = Build(s => s.AddIdentifiers());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(Ct));

        Assert.Contains("UsePlainCodec", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_Use_The_Last_Codec_Chosen() {
        using IHost host = Build(s => s.AddIdentifiers().UsePlainCodec().UseCodec(_ => TestCodecs.Aes('z')));

        await host.StartAsync(Ct);

        Assert.Equal('z', Assert.IsType<AesIdCodec>(IdCodec.Current).Version);
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task Should_Start_Several_Hosts_With_The_Same_Key_But_Not_A_Different_One() {
        using IHost first = Build(s => s.AddIdentifiers().UseAesCodec(o => o.AesKey = Base64Key));
        using IHost same = Build(s => s.AddIdentifiers().UseAesCodec(o => o.AesKey = Base64Key));
        using IHost different = Build(s => s.AddIdentifiers().UseAesCodec(o => o.AesKey = Convert.ToBase64String(TestCodecs.OtherKey)));

        await first.StartAsync(Ct);
        await same.StartAsync(Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => different.StartAsync(Ct));
    }

    [Fact]
    public void Should_Install_On_A_Provider_Built_Without_A_Host() {
        ServiceCollection services = new();
        services.AddIdentifiers().UsePlainCodec();
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(provider, provider.UseIdentifiers());
        Assert.Same(PlainIdCodec.Instance, IdCodec.Current);
    }

    [Fact]
    public void Should_Register_The_Installer_Once() {
        ServiceCollection services = new();
        services.AddIdentifiers().UsePlainCodec();
        services.AddIdentifiers().UsePlainCodec();

        Assert.Single(services, d => d.ServiceType == typeof(IHostedService));
        Assert.Single(services, d => d.ServiceType == typeof(IdCodec));
    }

    [Fact]
    public void Should_Return_A_Builder_Over_The_Same_Service_Collection() {
        ServiceCollection services = new();

        IIdentifiersBuilder builder = services.AddIdentifiers();

        Assert.Same(services, builder.Services);
        Assert.Same(builder, builder.UsePlainCodec());
        Assert.Same(builder, builder.UseAesCodec(o => o.AesKey = Base64Key));
    }

    [Fact]
    public void Should_Let_Another_Package_Choose_The_Codec_Through_The_Interface() {
        // A codec from outside this package (like UseKeyRingCodec) is an extension over UseCodec.
        ServiceCollection services = new();
        AesIdCodec custom = TestCodecs.Aes('c');
        services.AddIdentifiers().UsePlainCodec().UseCustomCodec(custom);
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.UseIdentifiers();

        Assert.Same(custom, IdCodec.Current);
    }

    [Fact]
    public void Should_Choose_The_Codec_In_The_Callback_Overload() {
        ServiceCollection services = new();

        IServiceCollection returned = services.AddIdentifiers(identifiers => identifiers.UsePlainCodec());
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.UseIdentifiers();

        Assert.Same(services, returned);
        Assert.Same(PlainIdCodec.Instance, IdCodec.Current);
    }

    [Fact]
    public void Should_Fail_On_The_Registration_Line_When_The_Callback_Chooses_No_Codec() {
        ServiceCollection services = new();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => services.AddIdentifiers(_ => { }));

        Assert.Contains("chose no codec", error.Message);
    }
}

file static class CustomCodecExtensions {
    public static IIdentifiersBuilder UseCustomCodec(this IIdentifiersBuilder builder, IdCodec codec) =>
        builder.UseCodec(_ => codec);
}
