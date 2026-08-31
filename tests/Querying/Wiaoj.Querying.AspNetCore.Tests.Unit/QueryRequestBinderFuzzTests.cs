using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Querying.AspNetCore.Binders;

namespace Wiaoj.Querying.AspNetCore.Tests.Unit;

/// <summary>
/// Aggressive fuzz and chaos testing suite for <see cref="QueryRequestBinder"/>,
/// verifying that hostile HTTP requests never crash the ASP.NET Core pipeline.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "ChaosAndFuzzing")]
public class QueryRequestBinderFuzzTests {
    private static DefaultHttpContext CreateContext() {
        ServiceCollection services = new();
        services.AddQuerying();
        ServiceProvider provider = services.BuildServiceProvider();

        DefaultHttpContext context = new() {
            RequestServices = provider
        };
        return context;
    }

    [Fact]
    public async Task Should_Gracefully_Handle_Hostile_And_Random_Http_Bodies_Without_Crashing() {
        // Arrange
        Random random = new(777);
        string[] methods = ["GET", "QUERY", "POST", "PUT", "DELETE", "INVALID_METHOD"];
        string?[] contentTypes = [
            "application/json",
            "text/plain",
            "application/x-www-form-urlencoded",
            "application/xml",
            "multipart/form-data",
            "random/garbage",
            null
        ];

        // Act: 10,000 chaotic HTTP requests
        for(int i = 0; i < 10_000; i++) {
            DefaultHttpContext context = CreateContext();
            context.Request.Method = methods[random.Next(methods.Length)];
            context.Request.ContentType = contentTypes[random.Next(contentTypes.Length)];

            int bodyLength = random.Next(0, 4096);
            byte[] bodyBytes = new byte[bodyLength];
            random.NextBytes(bodyBytes);

            context.Request.Body = new MemoryStream(bodyBytes);
            context.Request.ContentLength = bodyBytes.Length;

            try {
                _ = await QueryRequestBinder.BindAsync(context);
            }
            catch(BadHttpRequestException) {
                // Expected and correct: 415, 413, 400 status code exceptions
            }
            catch(Exception ex) {
                Assert.Fail($"Unhandled crash on iteration {i}: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}