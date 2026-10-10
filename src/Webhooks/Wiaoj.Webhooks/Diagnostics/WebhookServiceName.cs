using System.Reflection;

namespace Wiaoj.Webhooks.Diagnostics;

/// <summary>
/// Resolves the logical name of the service sending webhooks: the same for every instance (pod) of that service.
/// </summary>
/// <remarks>
/// Loop detection records this name in the <see cref="WebhookHeaderNames.WebhookCausalChain"/> header. It must not be an
/// instance identifier: a webhook that leaves pod 1 of a service and comes back to pod 2 has still returned to that service.
/// </remarks>
public static class WebhookServiceName {
    /// <summary>The OpenTelemetry environment variable naming the service (<c>OTEL_SERVICE_NAME</c>).</summary>
    public const string OpenTelemetryServiceNameVariable = "OTEL_SERVICE_NAME";

    /// <summary>The name used when neither the environment nor the entry assembly names the service.</summary>
    public const string Unknown = "unknown_service";

    /// <summary>
    /// Resolves the service name from <c>OTEL_SERVICE_NAME</c>, then the entry assembly's name, then <see cref="Unknown"/>.
    /// </summary>
    /// <returns>The resolved service name.</returns>
    public static string Resolve() {
        return Resolve(Environment.GetEnvironmentVariable(OpenTelemetryServiceNameVariable), Assembly.GetEntryAssembly());
    }

    internal static string Resolve(string? openTelemetryServiceName, Assembly? entryAssembly) {
        if(!string.IsNullOrWhiteSpace(openTelemetryServiceName)) {
            // The chain is comma-separated, so a comma inside a name would split it into two services.
            return openTelemetryServiceName.Trim().Replace(',', '_');
        }

        string? assemblyName = entryAssembly?.GetName().Name;
        return string.IsNullOrWhiteSpace(assemblyName) ? Unknown : assemblyName;
    }
}
