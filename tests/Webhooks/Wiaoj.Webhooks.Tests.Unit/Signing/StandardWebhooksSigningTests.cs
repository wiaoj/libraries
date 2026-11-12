using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using System.Text;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Webhooks.Signing;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Signing;

[Trait("Category", "Unit")]
[Trait("Feature", "Signing")]
[Trait("Component", "StandardWebhooks")]
public sealed class StandardWebhooksSigningTests {
    // The specification's own reference vector (standard-webhooks/standard-webhooks, the libraries' shared test).
    private const string SpecSecret = "whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw";
    private const string SpecId = "msg_p5jXN8AQM9LWM0D4loKWxJek";
    private const long SpecTimestamp = 1614265330;
    private const string SpecBody = """{"test": 2432232314}""";
    private const string SpecSignature = "v1,g0hM9SsE+OTPJTGt/tmIKtSyZlE3uFJELVlNIOLJ1OE=";

    private static byte[] SpecKey => StandardWebhooksSignature.KeyOf(Encoding.UTF8.GetBytes(SpecSecret));

    [Fact]
    public void Sign_MatchesTheSpecificationsReferenceVector() {
        Assert.Equal(SpecSignature, StandardWebhooksSignature.Sign(SpecId, SpecTimestamp, Encoding.UTF8.GetBytes(SpecBody), SpecKey));
    }

    [Fact]
    public void Verify_AcceptsTheReferenceVector_AmongOtherVersions_AndRefusesAnythingChanged() {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(SpecTimestamp);
        byte[] body = Encoding.UTF8.GetBytes(SpecBody);
        TimeSpan tolerance = TimeSpan.FromMinutes(5);

        Assert.True(StandardWebhooksSignature.Verify(SpecId, SpecTimestamp, body, $"v1a,abc {SpecSignature}", SpecKey, tolerance, now));
        Assert.False(StandardWebhooksSignature.Verify("msg_other", SpecTimestamp, body, SpecSignature, SpecKey, tolerance, now));
        Assert.False(StandardWebhooksSignature.Verify(SpecId, SpecTimestamp + 1, body, SpecSignature, SpecKey, tolerance, now));
        Assert.False(StandardWebhooksSignature.Verify(SpecId, SpecTimestamp, Encoding.UTF8.GetBytes("""{"test": 1}"""), SpecSignature, SpecKey, tolerance, now));
        Assert.False(StandardWebhooksSignature.Verify(SpecId, SpecTimestamp, body, SpecSignature, SpecKey, tolerance, now.AddMinutes(6)));
    }

    [Fact]
    public void FormatSecret_RoundTripsThroughKeyOf_AndASecretWithoutThePrefixIsItsOwnKey() {
        byte[] key = [1, 2, 3, 4, 5, 6, 7, 8];
        string secret = StandardWebhooksSignature.FormatSecret(key);

        Assert.StartsWith(StandardWebhooksSignature.SecretPrefix, secret);
        Assert.Equal(key, StandardWebhooksSignature.KeyOf(Encoding.UTF8.GetBytes(secret)));
        Assert.Equal("raw-key"u8.ToArray(), StandardWebhooksSignature.KeyOf("raw-key"u8));
        Assert.Throws<FormatException>(() => StandardWebhooksSignature.KeyOf("whsec_***"u8));
    }

    [Fact]
    public async Task TheMiddleware_SetsTheThreeHeaders_SignedUnderTheJobsId() {
        FakeSecretProtector<WebhookSigningContext> protector = new();
        FakeTimeProvider time = new(DateTimeOffset.FromUnixTimeSeconds(SpecTimestamp));
        StandardWebhooksSigningMiddleware middleware = new(protector, time);
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(
            jobId: WebhookJobId.Parse(SpecId),
            endpoint: WebhookTestFactory.CreateEndpoint(protector.Protect(SpecSecret)),
            serializedPayload: SpecBody);

        bool called = false;
        await middleware.InvokeAsync(context, (_, _) => { called = true; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

        Assert.True(called);
        IReadOnlyDictionary<string, string> headers = context.GetHeaders();
        Assert.Equal(SpecId, headers[StandardWebhooksSignature.IdHeader]);
        Assert.Equal(SpecTimestamp.ToString(System.Globalization.CultureInfo.InvariantCulture), headers[StandardWebhooksSignature.TimestampHeader]);
        Assert.Equal(SpecSignature, headers[StandardWebhooksSignature.SignatureHeader]);
    }

    [Fact]
    public async Task TheMiddleware_SignsUnderBothSecretsWhileOneIsRotated() {
        FakeSecretProtector<WebhookSigningContext> protector = new();
        FakeTimeProvider time = new(DateTimeOffset.FromUnixTimeSeconds(SpecTimestamp));
        StandardWebhooksSigningMiddleware middleware = new(protector, time);
        string next = StandardWebhooksSignature.FormatSecret([9, 8, 7, 6, 5, 4, 3, 2]);
        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint(protector.Protect(SpecSecret)) with {
            SecondarySecret = protector.Protect(next),
        };
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(jobId: WebhookJobId.Parse(SpecId), endpoint: endpoint, serializedPayload: SpecBody);

        await middleware.InvokeAsync(context, static (_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

        string header = context.GetHeaders()[StandardWebhooksSignature.SignatureHeader];
        byte[] body = Encoding.UTF8.GetBytes(SpecBody);
        DateTimeOffset now = time.GetUtcNow();
        Assert.StartsWith(SpecSignature + " v1,", header);
        Assert.True(StandardWebhooksSignature.Verify(SpecId, SpecTimestamp, body, header,
            StandardWebhooksSignature.KeyOf(Encoding.UTF8.GetBytes(next)), TimeSpan.FromMinutes(5), now));
    }

    [Fact]
    public void TheDeliveryPipeline_IsRegisteredByTheEngine_WithNothingAroundIt() {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddWiaojWebhooks();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IWebhookDeliveryPipeline>());
        Assert.Empty(provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>());
    }
}
