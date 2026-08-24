using System.Text;
using System.Text.Json.Serialization;
using Wiaoj.Primitives;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Webhooks;
using Wiaoj.Webhooks.AspNetCore.Context;
using Wiaoj.Webhooks.Inbound.Providers.GitHub;
using Wiaoj.Webhooks.Publishing;
using Wiaoj.Webhooks.Security;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => {
    options.SingleLine = true;
    options.TimestampFormat = "[HH:mm:ss] ";
});

const string githubInboundSecret = "ghsec_github_inbound_secret_key_12345";
FakeSecretProtector<WebhookSigningContext> secretProtector = new();
builder.Services.AddSingleton<ISecretProtector<WebhookSigningContext>>(secretProtector);

// Target Endpoints Configuration (Real RequestCatcher & Webhook.site URLs)
WebhookEndpointId requestcatcherEndpointId = new("ep-requestcatcher");
WebhookEndpointId secondaryEndpointId = new("ep-webhook-site");

Uri requestcatcherUrl = new("https://wiaoj.requestcatcher.com/test");
Uri secondaryUrl = new("https://webhook.site/653284a2-a810-451c-a768-25644faa17b6");

InMemoryEndpointResolver endpointResolver = new();
endpointResolver.Register(new WebhookEndpoint(requestcatcherEndpointId, requestcatcherUrl, secretProtector.Protect("whsec_outbound_key_1")));
endpointResolver.Register(new WebhookEndpoint(secondaryEndpointId, secondaryUrl, secretProtector.Protect("whsec_outbound_key_2")));
builder.Services.AddSingleton<IWebhookEndpointResolver>(endpointResolver);

builder.Services.AddWiaojWebhooks(webhooks => {
    webhooks.UseInMemoryTransport()
            .AllowPrivateNetworks()
            .UseStandardHeaders()
            .UseExponentialBackoffRetry()
            .UseContentDigest(ContentDigestAlgorithm.Sha512)
            .AddPublishing();

    webhooks.AddInbound(inbound => {
        inbound.AddPolicy("GitHub", policy => policy
            .WithSigner<GitHubWebhookSigner>() // Gercek GitHub HMAC dogrulamasi aktif
            .WithEventFromHeader("X-GitHub-Event") // Event adini X-GitHub-Event header'indan oku
            .WithTolerance(TimeSpan.FromMinutes(5))
            .UseSecret(Secret.From(githubInboundSecret)));
    });
});

WebApplication app = builder.Build();

// 2. Pre-seed Subscriptions (Outbound Fan-Out Kurallari)
IWebhookSubscriptionStore subscriptionStore = app.Services.GetRequiredService<IWebhookSubscriptionStore>();
await subscriptionStore.SaveSubscriptionAsync(new WebhookSubscription(requestcatcherEndpointId, "github.*") {
    Description = "RequestCatcher Destination"
});
await subscriptionStore.SaveSubscriptionAsync(new WebhookSubscription(secondaryEndpointId, "github.push") {
    Description = "Webhook.site Destination"
});

// 3. Map Inbound Multi-Event Webhook Hub (Tek URL -> Coklu Event)
app.MapWebhook("/api/webhooks/github")
   .UsePolicy("GitHub")
   .OnPing() // GitHub'in ilk gonderdigi "ping" webhook'unu 200 OK ile yakalar
   .On<GitHubPushWebhookEvent>("push", async (
       GitHubPushWebhookEvent @event,
       WebhookReceiverContext<GitHubPushWebhookEvent> context,
       IWebhookPublisher publisher,
       ILogger<Program> logger,
       CancellationToken ct) => {
           logger.LogInformation("Inbound GitHub PUSH verified. Repo: {Repo}, Pusher: {Pusher}",
               @event.Repository.FullName, @event.Pusher.Name);

           // Gelen push event'ini abone olan tum servislere (RequestCatcher / Webhook.site) fan-out et:
           IReadOnlyList<WebhookDeliveryHandle> handles = await publisher.PublishAsync(@event, ct);
           logger.LogInformation("Push event published to {Count} subscribers.", handles.Count);

           return Results.Ok(new { Status = "PushProcessed", DispatchedCount = handles.Count });
       })
   .On<GitHubIssuesWebhookEvent>("issues", async (
       GitHubIssuesWebhookEvent @event,
       ILogger<Program> logger) => {
           logger.LogInformation("Inbound GitHub ISSUE event received. Action: {Action}, Issue: #{Number} {Title}",
               @event.Action, @event.Issue.Number, @event.Issue.Title);

           return Results.Ok(new { Status = "IssueProcessed" });
       })
   .IgnoreUnhandledEvents(); // GitHub'in attigi diger 20 event (star, fork, release) icin 400 degil 200 OK don

// 4. Local Simulator Endpoints (GitHub sha256= Imzali Istek Ureticileri)
app.MapPost("/simulate-github-push", async (
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger,
    CancellationToken ct) => {
        // GitHub Push Payload JSON
        const string payloadJson = """
        {
          "ref": "refs/heads/main",
          "before": "0000000000000000000000000000000000000000",
          "after": "c7a91d4e00000000000000000000000000000000",
          "repository": {
            "id": 123456,
            "name": "webhooks",
            "full_name": "wiaoj/webhooks",
            "private": false,
            "html_url": "https://github.com/wiaoj/webhooks",
            "default_branch": "main"
          },
          "pusher": {
            "name": "bertan",
            "email": "bertan@wiaoj.com"
          },
          "sender": {
            "login": "bertan",
            "id": 1,
            "avatar_url": "https://github.com/images/error/octocat_happy.gif",
            "html_url": "https://github.com/bertan"
          },
          "created": false,
          "deleted": false,
          "forced": false,
          "compare": "https://github.com/wiaoj/webhooks/compare/main",
          "commits": []
        }
        """;

        GitHubWebhookSigner signer = new();
        UnixTimestamp now = UnixTimestamp.Now;
        WebhookSignature signature = signer.Sign(Encoding.UTF8.GetBytes(payloadJson), Encoding.UTF8.GetBytes(githubInboundSecret), now);

        HttpClient client = httpClientFactory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "http://localhost:5000/api/webhooks/github") {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-GitHub-Event", "push");
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature.Signature}");

        logger.LogInformation("Simulating incoming GitHub PUSH webhook POST request...");
        HttpResponseMessage response = await client.SendAsync(request, ct);
        string responseText = await response.Content.ReadAsStringAsync(ct);

        return Results.Ok(new {
            Event = "push",
            ReceiverStatusCode = (int)response.StatusCode,
            Response = responseText
        });
    });

app.MapPost("/simulate-github-issue", async (
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger,
    CancellationToken ct) => {
        const string payloadJson = """
        {
          "action": "opened",
          "issue": {
            "number": 42,
            "title": "Bug in rate limiter",
            "body": "Detailed description of the issue."
          },
          "repository": {
            "id": 123456,
            "name": "webhooks",
            "full_name": "wiaoj/webhooks"
          }
        }
        """;

        GitHubWebhookSigner signer = new();
        UnixTimestamp now = UnixTimestamp.Now;
        WebhookSignature signature = signer.Sign(Encoding.UTF8.GetBytes(payloadJson), Encoding.UTF8.GetBytes(githubInboundSecret), now);

        HttpClient client = httpClientFactory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "http://localhost:5000/api/webhooks/github") {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-GitHub-Event", "issues");
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature.Signature}");

        logger.LogInformation("Simulating incoming GitHub ISSUES webhook POST request...");
        HttpResponseMessage response = await client.SendAsync(request, ct);

        return Results.Ok(new {
            Event = "issues",
            ReceiverStatusCode = (int)response.StatusCode
        });
    });

app.Run("http://localhost:5000");

// Domain Event Definitions (where TEvent : class - IWebhookEvent zorunlu degil)
[WebhookEvent("github.push")]
public sealed record GitHubPushWebhookEvent(
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("before")] string Before,
    [property: JsonPropertyName("after")] string After,
    [property: JsonPropertyName("repository")] GitHubRepository Repository,
    [property: JsonPropertyName("pusher")] GitHubPusher Pusher,
    [property: JsonPropertyName("sender")] GitHubUser Sender,
    [property: JsonPropertyName("created")] bool Created,
    [property: JsonPropertyName("deleted")] bool Deleted,
    [property: JsonPropertyName("forced")] bool Forced,
    [property: JsonPropertyName("compare")] string CompareUrl,
    [property: JsonPropertyName("commits")] IReadOnlyList<GitHubCommit> Commits,
    [property: JsonPropertyName("head_commit")] GitHubCommit? HeadCommit) : IWebhookEvent;

public sealed record GitHubIssuesWebhookEvent(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("issue")] GitHubIssue Issue,
    [property: JsonPropertyName("repository")] GitHubRepositorySummary Repository);

public sealed record GitHubIssue(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body);

public sealed record GitHubRepositorySummary(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("full_name")] string FullName);

public sealed record GitHubRepository(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("private")] bool Private,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("default_branch")] string DefaultBranch);

public sealed record GitHubPusher(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("email")] string? Email);

public sealed record GitHubUser(
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("avatar_url")] string AvatarUrl,
    [property: JsonPropertyName("html_url")] string HtmlUrl);

public sealed record GitHubCommit(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("author")] GitHubCommitAuthor Author,
    [property: JsonPropertyName("added")] IReadOnlyList<string> Added,
    [property: JsonPropertyName("removed")] IReadOnlyList<string> Removed,
    [property: JsonPropertyName("modified")] IReadOnlyList<string> Modified);

public sealed record GitHubCommitAuthor(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("username")] string? Username);

internal sealed class InMemoryEndpointResolver : IWebhookEndpointResolver {
    private readonly Dictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = [];

    public void Register(WebhookEndpoint endpoint) {
        this._endpoints[endpoint.Id] = endpoint;
    }

    public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        this._endpoints.TryGetValue(endpointId, out WebhookEndpoint? endpoint);
        return ValueTask.FromResult(endpoint);
    }
}