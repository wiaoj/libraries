namespace Wiaoj.Primitives.Tests.Unit.UrnTests; 
public sealed class UrnParsingTests {
    [Theory]
    [InlineData("urn:user:123")]
    [InlineData("URN:user:123")] // Case-insensitive prefix
    [InlineData("urn:a-b-c:some:deep:path")]
    public void Parse_ValidStrings_ShouldSucceed(string input) {
        var urn = Urn.Parse(input);
        Assert.Equal(input.ToLower().StartsWith("urn:") ? input : input, urn.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("urn:")]
    [InlineData("urn:nid")]       // İkinci kolon eksik
    [InlineData("urn::nss")]      // NID boş
    [InlineData("urn:nid:")]      // NSS boş
    [InlineData("noturn:nid:nss")] // Yanlış prefix
    [InlineData("urn:nid_bad:nss")] // NID içinde geçersiz karakter
    public void TryParse_InvalidInputs_ShouldReturnFalse(string? input) {
        bool success = Urn.TryParse(input, null, out var result);
        Assert.False(success);
        Assert.Equal(Urn.Empty, result);
    }

    [Fact]
    public void Deconstruct_ShouldWorkCorrectly() {
        var urn = Urn.Create("user", "123");
        var (nid, nss) = urn;

        Assert.True(nid.SequenceEqual("user"));
        Assert.True(nss.SequenceEqual("123"));
    }
}