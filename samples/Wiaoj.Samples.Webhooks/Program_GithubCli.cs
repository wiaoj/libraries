//using System.Text.Json.Serialization;
//using Wiaoj.Primitives;
//using Wiaoj.Webhooks;
//using Wiaoj.Webhooks.AspNetCore;
//using Wiaoj.Webhooks.Inbound.Providers.GitHub;

//WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

//builder.Logging.ClearProviders();

//// 1. Secret Tanımı (GitHub CLI'a da aynı secret'ı vereceğiz)
//const string GitHubSecret = "my_super_secret_github_webhook_key_123";
//GitHubWebhookSigner githubSigner = new();

//builder.Services.AddWiaojWebhooks(webhooks => {
//    webhooks.UseInMemoryTransport()
//            .UseEndpointResolver((id, ct) => ValueTask.FromResult<WebhookEndpoint?>(null));
//});
//builder.Services.AddInboundWebhooks();

//WebApplication app = builder.Build();

//// 2. GitHub Ping / Push Event Endpoint'i
//app.MapWebhook<GitHubPingEvent>("/api/webhooks/github", (GitHubPingEvent @event, WebhookReceiverContext<GitHubPingEvent> ctx) => {
//    Console.WriteLine("\n=======================================================");
//    Console.WriteLine("🎉 [CANLI GITHUB WEBHOOK ALINDI VE DOĞRULANDI!]");
//    Console.WriteLine($"   Repo      : {@event.Repository?.FullName}");
//    Console.WriteLine($"   Zen Sözü  : {@event.Zen}");
//    Console.WriteLine($"   Header    : {ctx.Headers["X-GitHub-Event"]}");
//    Console.WriteLine($"   Raw Bytes : {ctx.RawBody.Length} bytes");
//    Console.WriteLine("=======================================================\n");

//    return Results.Ok(new { message = "Webhook received successfully" });
//})
//.WithSigner(githubSigner)
//.WithHeaderName(GitHubWebhookSigner.DefaultHeaderName)
//.WithSecret(Secret.From(GitHubSecret));

//app.Run("http://localhost:5000");

//// GitHub Ping Event Modeli
//public sealed record GitHubPingEvent : IWebhookEvent {
//    [JsonPropertyName("zen")]
//    public string? Zen { get; init; }

//    [JsonPropertyName("repository")]
//    public GitHubRepository? Repository { get; init; }
//}

//public sealed record GitHubRepository {
//    [JsonPropertyName("full_name")]
//    public string? FullName { get; init; }
//}