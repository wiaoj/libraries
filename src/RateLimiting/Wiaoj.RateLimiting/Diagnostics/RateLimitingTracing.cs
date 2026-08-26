using System.Diagnostics;
using System.Reflection;

namespace Wiaoj.RateLimiting.Diagnostics;

/// <summary>
/// Distributed tracing ActivitySource provider for rate limiting operations.
/// </summary>
internal static class RateLimitingTracing {
    public const string SourceName = "Wiaoj.RateLimiting";

    private static readonly string SourceVersion =
        typeof(RateLimitingTracing).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(RateLimitingTracing).Assembly.GetName().Version?.ToString()
        ?? "1.0.0";

    public static readonly ActivitySource Source = new(SourceName, SourceVersion);
}