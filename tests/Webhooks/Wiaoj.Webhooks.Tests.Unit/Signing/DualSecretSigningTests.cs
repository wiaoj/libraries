using Microsoft.Extensions.Time.Testing;
using System.Text;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Webhooks.Signing;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Signing;

/// <summary>
/// While a secret is rotated the endpoint holds two, and every delivery carries a signature from each, so the receiver
/// accepts whichever one it already has (#45).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Signing")]
[Trait("Component", "Middleware")]
public sealed class DualSecretSigningTests {
    private const string PrimaryPlain = "whsec_primary_key_material";
    private const string SecondaryPlain = "whsec_secondary_key_material";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly FakeSecretProtector<WebhookSigningContext> Protector = new();
    private static readonly HmacSha256WebhookSigner Signer = new();

    private static async Task<WebhookDeliveryContext> SignAsync(WebhookEndpoint endpoint, FakeTimeProvider time) {
        SigningMiddleware middleware = WebhookTestFactory.CreateSigningMiddleware(Signer, Protector, time);
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(endpoint);

        await middleware.InvokeAsync(context, static (_, _) => Task.CompletedTask, Ct);
        return context;
    }

    private static WebhookEndpoint EndpointWithBothSecrets() {
        return new WebhookEndpoint(
            WebhookTestFactory.CreateEndpointId(),
            WebhookTestFactory.CreateTargetUrl(),
            Protector.Protect(PrimaryPlain),
            Protector.Protect(SecondaryPlain),
            null,
            null);
    }

    private static bool Verifies(WebhookDeliveryContext context, string plainSecret, FakeTimeProvider time) {
        string header = context.GetHeader(Signer.HeaderName)!;
        return Signer.Verify(
            Encoding.UTF8.GetBytes(context.SerializedPayload),
            header,
            Encoding.UTF8.GetBytes(plainSecret),
            TimeSpan.FromMinutes(5),
            time.GetUnixTimestamp());
    }

    [Fact]
    public async Task TheHeaderCarriesBothSignatures_UnderOneTimestamp() {
        FakeTimeProvider time = new();

        WebhookDeliveryContext context = await SignAsync(EndpointWithBothSecrets(), time);
        string header = context.GetHeader(Signer.HeaderName)!;

        string[] parts = header.Split(',');
        Assert.Equal(3, parts.Length);
        Assert.StartsWith("t=", parts[0], StringComparison.Ordinal);
        Assert.StartsWith($"{Signer.SchemePrefix}=", parts[1], StringComparison.Ordinal);
        Assert.StartsWith($"{Signer.SchemePrefix}=", parts[2], StringComparison.Ordinal);
        Assert.NotEqual(parts[1], parts[2]);
        Assert.Equal($"t={time.GetUnixTimestamp().TotalSeconds}", parts[0]);
    }

    [Fact]
    public async Task EitherSecretVerifiesTheDelivery() {
        FakeTimeProvider time = new();

        WebhookDeliveryContext context = await SignAsync(EndpointWithBothSecrets(), time);

        Assert.True(Verifies(context, PrimaryPlain, time));
        Assert.True(Verifies(context, SecondaryPlain, time));
        Assert.False(Verifies(context, "whsec_a_third_secret_nobody_holds", time));
    }

    [Fact]
    public async Task WithoutASecondSecret_TheHeaderIsUnchanged() {
        FakeTimeProvider time = new();
        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint(Protector.Protect(PrimaryPlain));

        WebhookDeliveryContext context = await SignAsync(endpoint, time);
        string header = context.GetHeader(Signer.HeaderName)!;

        Assert.Equal(2, header.Split(',').Length);
        Assert.Equal(context.GetSignature()!.Value.HeaderValue, header);
        Assert.True(Verifies(context, PrimaryPlain, time));
        Assert.Null(endpoint.SecondarySecret);
    }

    [Fact]
    public async Task TheRecordedSignatureStaysThePrimaryOne() {
        FakeTimeProvider time = new();

        WebhookDeliveryContext context = await SignAsync(EndpointWithBothSecrets(), time);

        WebhookSignature recorded = context.GetSignature()!.Value;
        string header = context.GetHeader(Signer.HeaderName)!;
        Assert.StartsWith(recorded.HeaderValue, header, StringComparison.Ordinal);
        Assert.Equal(
            Signer.Sign(Encoding.UTF8.GetBytes(context.SerializedPayload), Encoding.UTF8.GetBytes(PrimaryPlain), time.GetUnixTimestamp()).Signature,
            recorded.Signature);
    }

    [Fact]
    public async Task ACustomEndpointSignerSignsBothSecrets() {
        FakeTimeProvider time = new();
        HmacSha512WebhookSigner custom = new();
        WebhookEndpoint endpoint = EndpointWithBothSecrets() with { CustomSigner = custom };

        SigningMiddleware middleware = WebhookTestFactory.CreateSigningMiddleware(Signer, Protector, time);
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(endpoint);
        await middleware.InvokeAsync(context, static (_, _) => Task.CompletedTask, Ct);

        string header = context.GetHeader(custom.HeaderName)!;
        string[] parts = header.Split(',');
        Assert.Equal(3, parts.Length);
        Assert.All(parts[1..], part => Assert.StartsWith($"{custom.SchemePrefix}=", part, StringComparison.Ordinal));
        Assert.True(custom.Verify(
            Encoding.UTF8.GetBytes(context.SerializedPayload),
            header,
            Encoding.UTF8.GetBytes(SecondaryPlain),
            TimeSpan.FromMinutes(5),
            time.GetUnixTimestamp()));
    }

    public sealed class TheEndpoint {
        [Fact]
        public void KeepsTheSecondSecretThroughWith() {
            WebhookEndpoint endpoint = EndpointWithBothSecrets();

            WebhookEndpoint moved = endpoint with { CustomHeaders = new Dictionary<string, string> { ["X-Trace"] = "1" } };

            Assert.Equal(endpoint.SecondarySecret, moved.SecondarySecret);
            Assert.NotNull(moved.CustomHeaders);
        }

        [Fact]
        public void RefusesADefaultSecondSecret() {
            Assert.ThrowsAny<ArgumentException>(() => new WebhookEndpoint(
                WebhookTestFactory.CreateEndpointId(),
                WebhookTestFactory.CreateTargetUrl(),
                Protector.Protect(PrimaryPlain),
                default(EncryptedSecret<WebhookSigningContext>),
                null,
                null));
        }

        [Fact]
        public void HasNoSecondSecret_WhenBuiltTheOldWay() {
            WebhookEndpoint endpoint = new(
                WebhookTestFactory.CreateEndpointId(),
                WebhookTestFactory.CreateTargetUrl(),
                Protector.Protect(PrimaryPlain),
                null,
                null);

            Assert.Null(endpoint.SecondarySecret);
        }
    }

    public sealed class TheBuilder {
        [Fact]
        public async Task CarriesBothSecretsOntoTheEndpoint() {
            WebhookEndpoint endpoint = await new WebhookEndpointBuilder()
                .WithId("ep_rotating")
                .WithTargetUrl("https://example.test/hooks")
                .WithSecret(PrimaryPlain, Protector)
                .WithSecondarySecret(SecondaryPlain, Protector)
                .WithSsrfValidation(validate: false)
                .BuildAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(endpoint.SecondarySecret);
            Assert.Equal(Protector.Protect(SecondaryPlain), endpoint.SecondarySecret!.Value);
        }

        [Fact]
        public async Task LeavesTheSecondSecretUnset_WhenNotConfigured() {
            WebhookEndpoint endpoint = await new WebhookEndpointBuilder()
                .WithId("ep_single")
                .WithTargetUrl("https://example.test/hooks")
                .WithSecret(PrimaryPlain, Protector)
                .WithSsrfValidation(validate: false)
                .BuildAsync(TestContext.Current.CancellationToken);

            Assert.Null(endpoint.SecondarySecret);
        }

        [Fact]
        public void RefusesAnEmptyOrDefaultSecondSecret() {
            WebhookEndpointBuilder builder = new();

            Assert.ThrowsAny<ArgumentException>(() => builder.WithSecondarySecret(default(EncryptedSecret<WebhookSigningContext>)));
            Assert.ThrowsAny<ArgumentException>(() => builder.WithSecondarySecret("  ", Protector));
            Assert.ThrowsAny<ArgumentException>(() => builder.WithSecondarySecret(SecondaryPlain, null!));
        }
    }
}
