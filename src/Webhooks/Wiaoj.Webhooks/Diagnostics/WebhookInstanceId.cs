namespace Wiaoj.Webhooks.Diagnostics;

/// <summary>
/// Provides utility methods for resolving or generating cloud-native instance identifiers across .NET hosting environments.
/// </summary>
public static class WebhookInstanceId {
    /// <summary>
    /// Resolves the instance identifier using environment metadata (Kubernetes HOSTNAME, Azure WEBSITE_INSTANCE_ID, OpenTelemetry),
    /// falling back to machine name and process ID with UUIDv7 entropy.
    /// </summary>
    /// <param name="rolePrefix">An optional role prefix (e.g. <c>"engine"</c>, <c>"worker"</c>).</param>
    /// <returns>A resolved or generated instance identifier string.</returns>
    public static string Resolve(string? rolePrefix = null) {
        // 1. OpenTelemetry standard environment variable
        string? otelInstanceId = Environment.GetEnvironmentVariable("OTEL_SERVICE_INSTANCE_ID");
        if(!string.IsNullOrWhiteSpace(otelInstanceId)) {
            return FormatWithPrefix(rolePrefix, otelInstanceId);
        }

        // 2. Azure App Service / Container Apps instance ID
        string? azureInstanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        if(!string.IsNullOrWhiteSpace(azureInstanceId)) {
            string shortAzureId = azureInstanceId.Length > 12 ? azureInstanceId[..12] : azureInstanceId;
            return FormatWithPrefix(rolePrefix, $"azure-{shortAzureId}");
        }

        // 3. Kubernetes Pod Name or Docker Container Hostname
        string? k8sPodName = Environment.GetEnvironmentVariable("HOSTNAME");
        if(!string.IsNullOrWhiteSpace(k8sPodName)) {
            return FormatWithPrefix(rolePrefix, k8sPodName);
        }

        // 4. Default Local / Generic VM Fallback (Machine:PID:UUIDv7)
        string machine = Environment.MachineName;
        int pid = Environment.ProcessId;
        string suffix = Guid.CreateVersion7().ToString("N")[..12];

        return FormatWithPrefix(rolePrefix, $"{machine}:{pid}:{suffix}");
    }

    private static string FormatWithPrefix(string? rolePrefix, string identity) {
        return string.IsNullOrWhiteSpace(rolePrefix)
            ? identity
            : $"{rolePrefix}@{identity}";
    }
}