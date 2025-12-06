using Wiaoj.Primitives.Snowflake;
using Xunit.Abstractions;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;

public class SnowflakeIntegrationTests {
    private readonly ITestOutputHelper _output;

    public SnowflakeIntegrationTests(ITestOutputHelper output) {
        this._output = output;
    }

    [Fact]
    public void Should_Convert_To_And_From_HexString() {
        // Arrange
        // Belirli bir ID üretelim (veya manuel verelim)
        // 1234567890123456789 (Long) -> 112210F47DE98115 (Hex)
        long rawValue = 1234567890123456789;
        SnowflakeId id = new(rawValue);

        // Act
        HexString hex = id.ToHexString();
        SnowflakeId idFromHex = SnowflakeId.From(hex);

        // Assert
        this._output.WriteLine($"Original: {id.Value}");
        this._output.WriteLine($"Hex:      {hex}");

        Assert.Equal("112210f47de98115", hex.ToLower().Value); // Big Endian kontrolü
        Assert.Equal(id, idFromHex);
    }

    [Fact]
    public void Should_Convert_To_And_From_Base64String() {
        // Arrange
        SnowflakeId id = SnowflakeId.NewId();

        // Act
        Base64String base64 = id.ToBase64String();
        SnowflakeId idFromBase64 = SnowflakeId.From(base64);

        // Assert
        this._output.WriteLine($"Original: {id.Value}");
        this._output.WriteLine($"Base64:   {base64}");

        // Base64 string boş olmamalı
        Assert.False(string.IsNullOrEmpty(base64.Value));
        // Geri dönüşüm eşit olmalı
        Assert.Equal(id, idFromBase64);
    }

    [Fact]
    public void Should_Convert_To_And_From_Base32String() {
        // Arrange
        SnowflakeId id = SnowflakeId.NewId();

        // Act
        Base32String base32 = id.ToBase32String();
        SnowflakeId idFromBase32 = SnowflakeId.From(base32);

        // Assert
        this._output.WriteLine($"Original: {id.Value}");
        this._output.WriteLine($"Base32:   {base32}");

        // Base32 genelde URL'ler için kullanılır, "=" padding içerebilir veya içermeyebilir
        // (Sizin Base32 implementasyonunuz padding kullanıyorsa '=' ile biter)
        Assert.Equal(id, idFromBase32);
    }

    [Fact]
    public void Validation_Should_Fail_On_Invalid_Lengths() {
        // SnowflakeId her zaman 8 byte olmalıdır.
        // Eğer 4 byte'lık bir veri verilirse hata fırlatmalı.

        HexString shortHex = HexString.FromBytes(new byte[] { 1, 2, 3, 4 });
        Assert.Throws<FormatException>(() => SnowflakeId.From(shortHex));

        Base64String shortBase64 = Base64String.FromBytes(new byte[] { 1, 2, 3, 4 });
        Assert.Throws<FormatException>(() => SnowflakeId.From(shortBase64));
    }

    [Fact]
    public void Should_Generate_Valid_Urn() {
        var id = new SnowflakeId(12345);
        // urn:nid:nss -> urn:order:12345
        var urn = id.ToUrn("order");

        Assert.Equal("urn:order:12345", urn.ToString());
    }
}