using Wiaoj.Serialization.DependencyInjection;

namespace Wiaoj.Serialization.Tests.Unit;

public sealed class SystemTextJsonContractTests : SerializerContractTests {
    protected override void Register(ISerializationBuilder builder) => builder.UseSystemTextJson<ContractKey>();
}

public sealed class MessagePackContractTests : SerializerContractTests {
    protected override void Register(ISerializationBuilder builder) => builder.UseMessagePack<ContractKey>();
}

public sealed class MemoryPackContractTests : SerializerContractTests {
    protected override void Register(ISerializationBuilder builder) => builder.UseMemoryPack<ContractKey>();
}

public sealed class BsonContractTests : SerializerContractTests {
    protected override void Register(ISerializationBuilder builder) => builder.UseBson<ContractKey>();
}

public sealed class YamlDotNetContractTests : SerializerContractTests {
    protected override void Register(ISerializationBuilder builder) => builder.UseYamlDotNet<ContractKey>();
}

public sealed class GzipCompressedContractTests : SerializerContractTests {
    protected override void Register(ISerializationBuilder builder) =>
        builder.UseSystemTextJson<ContractKey>().WithGzipCompression();
}

public sealed class BrotliCompressedContractTests : SerializerContractTests {
    protected override void Register(ISerializationBuilder builder) =>
        builder.UseSystemTextJson<ContractKey>().WithBrotliCompression();
}
