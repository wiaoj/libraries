using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Serialization.DependencyInjection;
using Wiaoj.Serialization.Transcoding;

namespace Wiaoj.Serialization.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Component", "Transcoding")]
public sealed class TranscodingTests {
    private readonly struct JsonKey : ISerializerKey;
    private readonly struct PackKey : ISerializerKey;

    [Fact]
    public void Converts_Bytes_From_One_Registered_Format_To_Another() {
        ServiceCollection services = new();
        services.AddWiaojSerializer(s => s
            .AddTranscoding()
            .UseSystemTextJson<JsonKey>());
        services.AddWiaojSerializer(s => s.UseMessagePack<PackKey>());
        using ServiceProvider provider = services.BuildServiceProvider();
        Order order = Order.Sample();
        byte[] json = provider.GetRequiredService<ISerializer<JsonKey>>().Serialize(order);

        byte[] pack = provider.GetRequiredService<ITranscoder>().From<JsonKey>(json).To<PackKey, Order>();

        Assert.Equivalent(order, provider.GetRequiredService<ISerializer<PackKey>>().Deserialize<Order>(pack), strict: true);
    }
}
