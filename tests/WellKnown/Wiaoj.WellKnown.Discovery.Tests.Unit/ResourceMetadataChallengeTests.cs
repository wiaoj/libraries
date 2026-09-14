namespace Wiaoj.WellKnown.Discovery.Tests.Unit;

/// <summary>Reading <c>resource_metadata</c> from challenges (RFC 9728 §5.1, RFC 9110 §11.6.1) — #114.</summary>
[Trait("Category", "Unit")]
[Trait("Feature", "WellKnown.Discovery")]
[Trait("Component", "ResourceMetadataChallenge")]
public sealed class ResourceMetadataChallengeTests {
    private const string Url = "https://api.example.com/.well-known/oauth-protected-resource";

    [Theory]
    [InlineData($"Bearer resource_metadata=\"{Url}\"")]
    [InlineData($"Bearer realm=\"api\", error=\"invalid_token\", error_description=\"expired, renew it\", resource_metadata=\"{Url}\"")]
    [InlineData($"DPoP algs=\"ES256 PS256\", resource_metadata=\"{Url}\"")]
    [InlineData($"Bearer RESOURCE_METADATA=\"{Url}\"")]
    [InlineData($"Bearer resource_metadata = \"{Url}\"")]
    [InlineData($"Bearer resource_metadata={Url}")]
    [InlineData($"Basic realm=\"x\", Bearer resource_metadata=\"{Url}\"")]
    public void Should_Find_The_Parameter_On_Any_Scheme_Beside_Other_Parameters(string header) {
        Assert.Equal(Url, ResourceMetadataChallenge.Find([header]));
    }

    [Fact]
    public void Should_Not_Be_Fooled_By_The_Name_Inside_Another_Parameters_Quoted_Value() {
        string header = "Bearer error_description=\"see resource_metadata=\\\"https://evil.example.com\\\"\"";

        Assert.Null(ResourceMetadataChallenge.Find([header]));
    }

    [Fact]
    public void Should_Unescape_A_Quoted_String() {
        Assert.Equal("https://api.example.com/a\"b", ResourceMetadataChallenge.Find(["Bearer resource_metadata=\"https://api.example.com/a\\\"b\""]));
    }

    [Fact]
    public void Should_Return_Null_Without_The_Parameter() {
        Assert.Null(ResourceMetadataChallenge.Find(["Bearer error=\"invalid_token\"", "Basic realm=\"x\""]));
        Assert.Null(ResourceMetadataChallenge.Find(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)));
    }

    [Fact]
    public void Should_Skip_A_Token68_Credential() {
        Assert.Equal(Url, ResourceMetadataChallenge.Find(["Negotiate abc==", $"Bearer resource_metadata=\"{Url}\""]));
    }

    [Fact]
    public void Should_Accept_The_Same_Url_Advertised_By_Several_Challenges() {
        Assert.Equal(Url, ResourceMetadataChallenge.Find([$"Bearer resource_metadata=\"{Url}\"", $"DPoP resource_metadata=\"{Url}\""]));
    }

    [Fact]
    public void Should_Refuse_Different_Urls() {
        OAuthDiscoveryException error = Assert.Throws<OAuthDiscoveryException>(() => ResourceMetadataChallenge.Find(
            [$"Bearer resource_metadata=\"{Url}\"", "DPoP resource_metadata=\"https://other.example.com/.well-known/oauth-protected-resource\""]));

        Assert.Equal(OAuthDiscoveryFailure.AmbiguousChallenge, error.Failure);
    }

    [Fact]
    public void Should_Read_The_Header_Of_A_Response() {
        HttpResponseMessage response = FakeMetadataServer.Challenge("https://api.example.com/users", $"Bearer error=\"invalid_token\", resource_metadata=\"{Url}\"");

        Assert.Equal(Url, ResourceMetadataChallenge.Find(response));
    }
}
