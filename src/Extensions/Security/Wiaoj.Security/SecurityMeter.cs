using System.Diagnostics.Metrics;
using System.Reflection;

namespace Wiaoj.Security;

/// <summary>
/// Central <see cref="Meter"/> and instruments for the Wiaoj security system.
/// Uses <c>System.Diagnostics.Metrics</c> — the .NET built-in metrics API.
/// Compatible with OpenTelemetry, dotnet-counters, Prometheus exporters, etc.
/// without taking any dependency on them.
/// </summary>
/// <remarks>
/// <b>Instrument naming</b> follows OpenTelemetry semantic conventions:
/// <c>{namespace}.{entity}.{verb}</c> in snake_case.<br/>
/// <b>Context tag</b>: every record call includes a <c>context</c> tag
/// (e.g. <c>WebhookSigningContext</c>) so dashboards can break down by domain.
/// </remarks>
public static class SecurityMeter {
    /// <summary>
    /// Meter name — use this when subscribing in tests or configuring exporters:
    /// <code>
    /// services.AddOpenTelemetry().WithMetrics(m => m.AddMeter(SecurityMeter.Name));
    /// </code>
    /// </summary>
    public const string Name = "Wiaoj.Security";
    public static readonly string Version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "1.0.0";

    private static readonly Meter _meter = new(Name, Version);

    // ── Protect / Unprotect ───────────────────────────────────────────────────

    /// <summary>Total number of successful <c>Protect</c> calls, tagged by context.</summary>
    public static readonly Counter<long> ProtectCount =
        _meter.CreateCounter<long>(
            "wiaoj.security.protect.count",
            unit: "{operations}",
            description: "Total number of successful Protect (encrypt) operations.");

    /// <summary>Total number of failed <c>Protect</c> calls, tagged by context.</summary>
    public static readonly Counter<long> ProtectErrorCount =
        _meter.CreateCounter<long>(
            "wiaoj.security.protect.error.count",
            unit: "{operations}",
            description: "Total number of failed Protect operations.");

    /// <summary>Duration of <c>Protect</c> calls in milliseconds.</summary>
    public static readonly Histogram<double> ProtectDuration =
        _meter.CreateHistogram<double>(
            "wiaoj.security.protect.duration",
            unit: "ms",
            description: "Duration of Protect (encrypt) operations in milliseconds.");

    /// <summary>Total number of successful <c>Unprotect</c> calls, tagged by context.</summary>
    public static readonly Counter<long> UnprotectCount =
        _meter.CreateCounter<long>(
            "wiaoj.security.unprotect.count",
            unit: "{operations}",
            description: "Total number of successful Unprotect (decrypt) operations.");

    /// <summary>Total number of failed <c>Unprotect</c> calls (auth failures, tampering).</summary>
    public static readonly Counter<long> UnprotectErrorCount =
        _meter.CreateCounter<long>(
            "wiaoj.security.unprotect.error.count",
            unit: "{operations}",
            description: "Total number of failed Unprotect operations (auth failures, tampering).");

    /// <summary>Duration of <c>Unprotect</c> calls in milliseconds.</summary>
    public static readonly Histogram<double> UnprotectDuration =
        _meter.CreateHistogram<double>(
            "wiaoj.security.unprotect.duration",
            unit: "ms",
            description: "Duration of Unprotect (decrypt) operations in milliseconds.");

    // ── Key Rotation ──────────────────────────────────────────────────────────

    /// <summary>Total number of key rotations completed successfully.</summary>
    public static readonly Counter<long> RotationCount =
        _meter.CreateCounter<long>(
            "wiaoj.security.rotation.count",
            unit: "{rotations}",
            description: "Total number of successful key rotation cycles.");

    /// <summary>Total number of key rotation failures.</summary>
    public static readonly Counter<long> RotationErrorCount =
        _meter.CreateCounter<long>(
            "wiaoj.security.rotation.error.count",
            unit: "{rotations}",
            description: "Total number of key rotation failures.");

    /// <summary>Duration of a full key rotation cycle in milliseconds.</summary>
    public static readonly Histogram<double> RotationDuration =
        _meter.CreateHistogram<double>(
            "wiaoj.security.rotation.duration",
            unit: "ms",
            description: "Duration of a full key rotation cycle in milliseconds.");

    // ── Master-key Rewrap (Type B) ────────────────────────────────────────────

    /// <summary>Total number of completed master-key rewrap cycles.</summary>
    public static readonly Counter<long> RewrapCount =
        _meter.CreateCounter<long>(
            "wiaoj.security.rewrap.count",
            unit: "{rewraps}",
            description: "Total number of successful master-key (Type B) rewrap cycles.");

    /// <summary>Total number of failed master-key rewrap cycles.</summary>
    public static readonly Counter<long> RewrapErrorCount =
        _meter.CreateCounter<long>(
            "wiaoj.security.rewrap.error.count",
            unit: "{rewraps}",
            description: "Total number of failed master-key rewrap cycles.");

    /// <summary>Number of individual key records re-wrapped during rewrap cycles.</summary>
    public static readonly Counter<long> RewrapKeyCount =
        _meter.CreateCounter<long>(
            "wiaoj.security.rewrap.key.count",
            unit: "{keys}",
            description: "Number of individual encryption key records re-wrapped with the new master key.");

    /// <summary>Duration of a full master-key rewrap cycle in milliseconds.</summary>
    public static readonly Histogram<double> RewrapDuration =
        _meter.CreateHistogram<double>(
            "wiaoj.security.rewrap.duration",
            unit: "ms",
            description: "Duration of a full master-key rewrap cycle in milliseconds.");

    // ── Key Ring ──────────────────────────────────────────────────────────────

    /// <summary>Total number of key ring reloads (startup + post-rotation).</summary>
    public static readonly Counter<long> KeyRingReloadCount =
        _meter.CreateCounter<long>(
            "wiaoj.security.keyring.reload.count",
            unit: "{reloads}",
            description: "Total number of key ring reload operations.");

    // ── Tag helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the standard context tag for a given <typeparamref name="TContext"/>.
    /// Centralised here so all callers use the same tag key/value.
    /// </summary>
    public static KeyValuePair<string, object?> ContextTag<TContext>() where TContext : ISecretContext {
        return new("context", typeof(TContext).Name);
    }
}