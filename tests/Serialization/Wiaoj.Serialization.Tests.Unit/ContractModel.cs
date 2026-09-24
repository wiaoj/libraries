using MemoryPack;
using MessagePack;

namespace Wiaoj.Serialization.Tests.Unit;

/// <summary>The key every contract test registers its serializer under.</summary>
public readonly struct ContractKey : ISerializerKey;

/// <summary>
/// A value every format can carry: no Guid (BSON needs a representation configured), no DateTimeOffset (BSON writes
/// it as an array), settable properties and a parameterless constructor (YAML, BSON).
/// </summary>
[MemoryPackable]
[MessagePackObject(keyAsPropertyName: true)]
public sealed partial class Order {
    public int Id { get; set; }
    public string Customer { get; set; } = "";
    public double Total { get; set; }
    public bool Paid { get; set; }
    public List<string> Tags { get; set; } = [];
    public Address? ShipTo { get; set; }

    public static Order Sample() => new() {
        Id = 42,
        Customer = "Ada Lovelace — ğüşıöç",
        Total = 1234.5,
        Paid = true,
        Tags = ["priority", "gift"],
        ShipTo = new Address { City = "İstanbul", Line = "Bağdat Cd. 1" }
    };
}

[MemoryPackable]
[MessagePackObject(keyAsPropertyName: true)]
public sealed partial class Address {
    public string City { get; set; } = "";
    public string Line { get; set; } = "";
}
