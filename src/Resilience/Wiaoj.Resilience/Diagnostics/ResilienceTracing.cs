using System.Diagnostics;
using System.Reflection;

namespace Wiaoj.Resilience.Diagnostics;

/// <summary>
/// Distributed tracing ActivitySource provider for circuit breaker operations.
/// </summary>
internal static class ResilienceTracing {
    public const string SourceName = "Wiaoj.Resilience";

    private static readonly string SourceVersion =
        typeof(ResilienceTracing).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ResilienceTracing).Assembly.GetName().Version?.ToString()
        ?? "1.0.0";

    public static readonly ActivitySource Source = new(SourceName, SourceVersion);
}