using System.Buffers.Binary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization.DependencyInjection;
using Wiaoj.Serialization.Security;

namespace Wiaoj.Serialization.Tests.Unit;

public sealed class OrdersContext : ISecretContext;

public sealed class OtherContext : ISecretContext;

/// <summary>A real managed protector — AES-GCM, key ring, rotation — over a fake master key and an in-memory store.</summary>
internal static class TestSecurity {
    public static IServiceCollection AddTestProtector<TContext>(this IServiceCollection services) where TContext : ISecretContext {
        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddWiaojSecurity().AddManagedProtector<TContext>();
        services.Replace(ServiceDescriptor.Singleton<IMasterKeyProvider, FakeMasterKeyProvider>());
        services.Replace(ServiceDescriptor.Singleton<IEncryptionKeyStore, InMemoryEncryptionKeyStore>());
        return services;
    }
}

public sealed class EncryptedJsonContractTests : SerializerContractTests {
    protected override bool IsDeterministic => false;

    protected override void ConfigureServices(IServiceCollection services) => services.AddTestProtector<OrdersContext>();

    protected override void Register(ISerializationBuilder builder) =>
        builder.UseSystemTextJson<ContractKey>().WithEncryption<ContractKey, OrdersContext>();
}

/// <summary>A binary inner format: its text form is base64, so text and bytes must not be mixed up.</summary>
public sealed class EncryptedMessagePackContractTests : SerializerContractTests {
    protected override bool IsDeterministic => false;

    protected override void ConfigureServices(IServiceCollection services) => services.AddTestProtector<OrdersContext>();

    protected override void Register(ISerializationBuilder builder) =>
        builder.UseMessagePack<ContractKey>().WithEncryption<ContractKey, OrdersContext>();
}

[Trait("Category", "Unit")]
[Trait("Component", "Encryption")]
public sealed class EncryptionTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiceProvider Build<TContext>() where TContext : ISecretContext {
        ServiceCollection services = new();
        services.AddTestProtector<TContext>();
        services.AddWiaojSerializer(s => s.UseSystemTextJson<ContractKey>().WithEncryption<ContractKey, TContext>());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Output_Is_Not_The_Plain_Json_And_Starts_With_The_Key_Version() {
        await using ServiceProvider provider = Build<OrdersContext>();
        ISerializer<ContractKey> serializer = provider.GetRequiredService<ISerializer<ContractKey>>();

        byte[] bytes = serializer.Serialize(Order.Sample());

        Assert.DoesNotContain("Ada Lovelace", System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(bytes));
        Assert.StartsWith("v1.", serializer.SerializeToString(Order.Sample()));
    }

    [Fact]
    public async Task A_Flipped_Byte_Is_Refused() {
        await using ServiceProvider provider = Build<OrdersContext>();
        ISerializer<ContractKey> serializer = provider.GetRequiredService<ISerializer<ContractKey>>();
        byte[] bytes = serializer.Serialize(Order.Sample());

        bytes[^1] ^= 0x01;

        Assert.Throws<DecryptionFailedException>(() => serializer.Deserialize<Order>(bytes));
    }

    [Fact]
    public async Task An_Unknown_Key_Version_Is_Refused() {
        await using ServiceProvider provider = Build<OrdersContext>();
        ISerializer<ContractKey> serializer = provider.GetRequiredService<ISerializer<ContractKey>>();
        byte[] bytes = serializer.Serialize(Order.Sample());

        BinaryPrimitives.WriteInt32BigEndian(bytes, 99);

        Assert.Throws<DecryptionFailedException>(() => serializer.Deserialize<Order>(bytes));
    }

    [Fact]
    public async Task Data_From_Another_Context_Is_Refused() {
        await using ServiceProvider orders = Build<OrdersContext>();
        await using ServiceProvider other = Build<OtherContext>();
        byte[] bytes = orders.GetRequiredService<ISerializer<ContractKey>>().Serialize(Order.Sample());

        Assert.Throws<DecryptionFailedException>(() => other.GetRequiredService<ISerializer<ContractKey>>().Deserialize<Order>(bytes));
    }

    [Fact]
    public async Task Data_Written_Before_A_Rotation_Stays_Readable_And_New_Data_Uses_The_New_Key() {
        await using ServiceProvider provider = Build<OrdersContext>();
        ISerializer<ContractKey> serializer = provider.GetRequiredService<ISerializer<ContractKey>>();
        Order order = Order.Sample();
        byte[] before = serializer.Serialize(order);
        string beforeText = serializer.SerializeToString(order);

        await using(AsyncServiceScope scope = provider.CreateAsyncScope()) {
            await scope.ServiceProvider.GetRequiredService<KeyRotationService<OrdersContext>>().ForceRotateAsync(Ct);
        }

        byte[] after = serializer.Serialize(order);
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(before));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(after));
        Assert.Equivalent(order, serializer.Deserialize<Order>(before), strict: true);
        Assert.Equivalent(order, serializer.DeserializeFromString<Order>(beforeText), strict: true);
        Assert.Equivalent(order, serializer.Deserialize<Order>(after), strict: true);
    }
}
