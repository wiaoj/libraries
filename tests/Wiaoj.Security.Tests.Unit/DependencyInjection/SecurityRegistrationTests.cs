using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Wiaoj.Security.Tests.Unit.DependencyInjection;

public sealed class SecurityRegistrationTests {
    [Fact]
    public void ConfigureKeyRotation_Sets_The_Options() {
        ServiceCollection services = new();

        services.AddWiaojSecurity().ConfigureKeyRotation(o => o.KeySizeInBits = 128);
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal(128, provider.GetRequiredService<IOptions<KeyRotationOptions>>().Value.KeySizeInBits);
    }

    [Fact]
    public void ConfigureKeyRotation_Binds_From_Configuration() {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["KeySizeInBits"] = "192",
                ["RotationInterval"] = "30.00:00:00"
            })
            .Build();
        ServiceCollection services = new();

        services.AddWiaojSecurity().ConfigureKeyRotation(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();
        KeyRotationOptions options = provider.GetRequiredService<IOptions<KeyRotationOptions>>().Value;

        Assert.Equal(192, options.KeySizeInBits);
        Assert.Equal(TimeSpan.FromDays(30), options.RotationInterval);
    }

    [Fact]
    public void Invalid_Key_Rotation_Options_Fail_Validation() {
        ServiceCollection services = new();

        services.AddWiaojSecurity(security => security.ConfigureKeyRotation(o => o.KeySizeInBits = 100));
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<KeyRotationOptions>>().Value);
    }

    [Fact]
    public void Callback_Overload_Passes_A_Builder_Over_The_Same_Services_And_Returns_Them() {
        ServiceCollection services = new();
        ISecurityBuilder? configured = null;

        IServiceCollection returned = services.AddWiaojSecurity(security => configured = security);

        Assert.Same(services, returned);
        Assert.Same(services, configured!.Services);
    }
}
