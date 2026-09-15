using System.Security.Cryptography;
using Wiaoj.Primitives.Snowflake;
using Wiaoj.Security;

namespace Wiaoj.Identifiers.Security.Tests.Unit;

[Identifier("inv")]
public readonly partial record struct InvoiceId;

public sealed class IdentifierContext : ISecretContext;

/// <summary>A key ring in memory: random key material per version, derived the way <c>EncryptionKey</c> does.</summary>
internal sealed class FakeSubkeyDeriver : ISubkeyDeriver<IdentifierContext> {
    private readonly Dictionary<int, byte[]> _keys = [];

    public FakeSubkeyDeriver(params int[] versions) {
        foreach(int version in versions) {
            this.Add(version);
        }

        this.CurrentKeyVersion = KeyVersion.Of(versions.Max());
    }

    public KeyVersion CurrentKeyVersion { get; set; }

    public IReadOnlyCollection<KeyVersion> KeyVersions => [.. this._keys.Keys.Order().Select(KeyVersion.Of)];

    public int Derivations { get; private set; }

    public void Add(int version) => this._keys[version] = RandomNumberGenerator.GetBytes(32);

    public void Remove(int version) => this._keys.Remove(version);

    public byte[] KeyOf(int version) => this._keys[version];

    public void DeriveSubkey(KeyVersion version, ReadOnlySpan<byte> purpose, Span<byte> destination) {
        if(!this._keys.TryGetValue(version.Value, out byte[]? key)) {
            throw new KeyNotFoundException($"No key {version}.");
        }

        this.Derivations++;
        HKDF.DeriveKey(HashAlgorithmName.SHA256, key, destination, salt: [], info: [.. "wiaoj.security.subkey:"u8, .. purpose]);
    }
}

/// <summary>
/// Identifiers are encrypted under the key ring's current version and read back under any version still in the ring
/// (#165).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Identifiers")]
[Trait("Component", "KeyRingIdCodec")]
public sealed class KeyRingIdCodecTests {
    private static readonly SnowflakeId Value = new(987654321);

    [Fact]
    public void Should_Encrypt_With_The_Current_Version_As_The_Aes_Codec_Keyed_By_The_Identifiers_Subkey() {
        FakeSubkeyDeriver keys = new(1, 2);
        KeyRingIdCodec<IdentifierContext> codec = new(keys);

        byte[] subkey = HKDF.DeriveKey(HashAlgorithmName.SHA256, keys.KeyOf(2), 32, [], [.. "wiaoj.security.subkey:"u8, .. "wiaoj.identifiers"u8]);
        string expected = new AesIdCodec(subkey, '2').Encode("inv", Value);

        Assert.Equal(expected, codec.Encode("inv", Value));
        Assert.StartsWith("inv_2", expected, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_Keep_Reading_Identifiers_Written_Before_A_Rotation() {
        FakeSubkeyDeriver keys = new(1);
        KeyRingIdCodec<IdentifierContext> codec = new(keys);
        string beforeRotation = codec.Encode("inv", Value);

        keys.Add(2);
        keys.CurrentKeyVersion = KeyVersion.Of(2);
        string afterRotation = codec.Encode("inv", Value);

        Assert.StartsWith("inv_1", beforeRotation, StringComparison.Ordinal);
        Assert.StartsWith("inv_2", afterRotation, StringComparison.Ordinal);
        Assert.NotEqual(beforeRotation["inv_1".Length..], afterRotation["inv_2".Length..]);
        Assert.True(codec.TryDecode("inv", beforeRotation, out SnowflakeId old) && old == Value);
        Assert.True(codec.TryDecode("inv", afterRotation, out SnowflakeId current) && current == Value);
    }

    [Fact]
    public void Should_Refuse_An_Identifier_Whose_Version_Left_The_Ring() {
        FakeSubkeyDeriver keys = new(1, 2);
        keys.CurrentKeyVersion = KeyVersion.Of(1);
        KeyRingIdCodec<IdentifierContext> codec = new(keys);
        string written = codec.Encode("inv", Value);
        Assert.True(codec.TryDecode("inv", written, out _));

        keys.CurrentKeyVersion = KeyVersion.Of(2);
        keys.Remove(1);

        Assert.False(codec.TryDecode("inv", written, out _));
    }

    [Theory]
    [InlineData(0, '0')]
    [InlineData(1, '1')]
    [InlineData(9, '9')]
    [InlineData(10, 'A')]
    [InlineData(35, 'Z')]
    [InlineData(36, 'a')]
    [InlineData(61, 'z')]
    [InlineData(62, '0')]
    [InlineData(63, '1')]
    [InlineData(1000, '8')]
    public void Should_Write_The_Base62_Digit_Of_The_Version_Modulo_62(int version, char expected) {
        Assert.Equal(expected, KeyRingIdCodec<IdentifierContext>.VersionCharacter(KeyVersion.Of(version)));
    }

    [Fact]
    public void Should_Read_Both_Versions_That_Share_A_Character() {
        FakeSubkeyDeriver keys = new(1, 63);
        KeyRingIdCodec<IdentifierContext> codec = new(keys);

        keys.CurrentKeyVersion = KeyVersion.Of(1);
        string fromVersion1 = codec.Encode("inv", new SnowflakeId(1));
        keys.CurrentKeyVersion = KeyVersion.Of(63);
        string fromVersion63 = codec.Encode("inv", new SnowflakeId(63));

        Assert.Equal('1', fromVersion1["inv_".Length]);
        Assert.Equal('1', fromVersion63["inv_".Length]);
        Assert.True(codec.TryDecode("inv", fromVersion1, out SnowflakeId first) && first.Value == 1);
        Assert.True(codec.TryDecode("inv", fromVersion63, out SnowflakeId second) && second.Value == 63);
    }

    [Fact]
    public void Should_Refuse_Identifiers_From_Another_Key_Ring_Or_Prefix_And_Forgeries() {
        FakeSubkeyDeriver keys = new(1);
        KeyRingIdCodec<IdentifierContext> codec = new(keys);
        string written = codec.Encode("inv", Value);

        Assert.False(new KeyRingIdCodec<IdentifierContext>(new FakeSubkeyDeriver(1)).TryDecode("inv", written, out _));
        Assert.False(codec.TryDecode("ord", "ord" + written["inv".Length..], out _));
        Assert.False(codec.TryDecode("inv", written[..^1] + (written[^1] == 'a' ? 'b' : 'a'), out _));
        Assert.False(codec.TryDecode("inv", written[..^1], out _));
        Assert.False(codec.TryDecode("inv", "inv_", out _));
        Assert.False(codec.TryDecode("inv", "inv_9" + written["inv_1".Length..], out _));
    }

    [Fact]
    public void Should_Derive_Each_Version_Only_Once() {
        FakeSubkeyDeriver keys = new(1, 2);
        KeyRingIdCodec<IdentifierContext> codec = new(keys);
        keys.CurrentKeyVersion = KeyVersion.Of(1);
        string one = codec.Encode("inv", Value);
        keys.CurrentKeyVersion = KeyVersion.Of(2);

        for(int i = 0; i < 100; i++) {
            codec.TryDecode("inv", one, out _);
            codec.Encode("inv", new SnowflakeId(i));
        }

        Assert.Equal(2, keys.Derivations);
    }

    [Fact]
    public void Should_Be_Equivalent_Only_Over_The_Same_Key_Source() {
        FakeSubkeyDeriver keys = new(1);

        Assert.True(new KeyRingIdCodec<IdentifierContext>(keys).IsEquivalentTo(new KeyRingIdCodec<IdentifierContext>(keys)));
        Assert.False(new KeyRingIdCodec<IdentifierContext>(keys).IsEquivalentTo(new KeyRingIdCodec<IdentifierContext>(new FakeSubkeyDeriver(1))));
        Assert.False(new KeyRingIdCodec<IdentifierContext>(keys).IsEquivalentTo(PlainIdCodec.Instance));
    }

    [Fact]
    public void Should_Work_As_The_Codec_Of_Generated_Identifiers() {
        KeyRingIdCodec<IdentifierContext> codec = new(new FakeSubkeyDeriver(3));
        using IDisposable scope = IdCodec.Override(codec);

        InvoiceId id = InvoiceId.New();
        string text = id.ToString();

        Assert.StartsWith("inv_3", text, StringComparison.Ordinal);
        Assert.Equal(id, InvoiceId.Parse(text));
        Assert.Equal(codec.GetMaxEncodedLength(InvoiceId.Prefix), text.Length);
    }

    [Fact]
    public void Should_Refuse_A_Missing_Key_Source() {
        Assert.ThrowsAny<ArgumentNullException>(() => new KeyRingIdCodec<IdentifierContext>(null!));
    }
}
