using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Serialization.DependencyInjection;

namespace Wiaoj.Serialization.DependencyInjection.Tests.Unit;

/// <summary>
/// The non-keyed <see cref="ISerializer"/> is the keyless serializer, otherwise the only one registered, otherwise none —
/// and it is chosen when resolved, so serializers added through the fluent builder after
/// <c>AddWiaojSerializer()</c> returns count too.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
public sealed class DefaultSerializerTests {
    private readonly struct Orders : ISerializerKey;
    private readonly struct Audit : ISerializerKey;

    [Fact]
    public void Fluent_OnlyKeyedSerializer_IsTheDefault() {
        ServiceCollection services = new();
        services.AddWiaojSerializer().UseSystemTextJson<Orders>();
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<ISerializer<Orders>>(), provider.GetRequiredService<ISerializer>());
    }

    [Fact]
    public void Callback_OnlyKeyedSerializer_IsTheDefault() {
        ServiceCollection services = new();
        IServiceCollection returned = services.AddWiaojSerializer(s => s.UseSystemTextJson<Orders>());
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(services, returned);
        Assert.Same(provider.GetRequiredService<ISerializer<Orders>>(), provider.GetRequiredService<ISerializer>());
    }

    [Fact]
    public void Keyless_Wins_Over_Keyed() {
        ServiceCollection services = new();
        ISerializationBuilder builder = services.AddWiaojSerializer();
        builder.UseSystemTextJson<Orders>();
        builder.UseSystemTextJson();
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<ISerializer<KeylessRegistration>>(), provider.GetRequiredService<ISerializer>());
    }

    [Fact]
    public void Several_Keyed_And_No_Keyless_Means_No_Default() {
        ServiceCollection services = new();
        services.AddWiaojSerializer().UseSystemTextJson<Orders>();
        services.AddWiaojSerializer(s => s.UseSystemTextJson<Audit>());
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<ISerializer>());
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ISerializer>());
        Assert.NotNull(provider.GetRequiredService<ISerializer<Orders>>());
        Assert.NotNull(provider.GetRequiredService<ISerializer<Audit>>());
    }

    [Fact]
    public void Calling_Twice_Registers_One_Default_And_One_Provider() {
        ServiceCollection services = new();
        services.AddWiaojSerializer();
        services.AddWiaojSerializer(s => s.UseSystemTextJson<Orders>());

        Assert.Single(services, d => d.ServiceType == typeof(ISerializer));
        Assert.Single(services, d => d.ServiceType == typeof(ISerializerProvider));
    }
}
