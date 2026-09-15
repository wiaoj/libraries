using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;

namespace Wiaoj.Net;

/// <summary>
/// The <c>Wiaoj.Net</c> meter: refused outbound connections and DNS resolution time.
/// </summary>
/// <remarks>
/// Tags are bounded on purpose. Host, port and address come from whoever supplied the URL — an attacker can make every
/// value unique and blow up a metrics backend — so they are never tags; the reason and the address scope are small fixed
/// sets.
/// </remarks>
internal static class OutboundNetworkMeter {
    public const string Name = "Wiaoj.Net";

    public const string RefusedName = "wiaoj.net.outbound.refused";
    public const string DnsResolutionDurationName = "wiaoj.net.dns.resolution.duration";

    public const string ReasonTag = "reason";
    public const string ScopeTag = "scope";
    public const string OutcomeTag = "outcome";

    private static readonly Meter Meter = new(Name, typeof(OutboundNetworkMeter).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    private static readonly Counter<long> Refused = Meter.CreateCounter<long>(
        RefusedName,
        unit: "{connection}",
        description: "Outbound connections refused by the outbound network policy before a socket was opened. " +
                     "Counts connections, not requests or deliveries.");

    private static readonly Histogram<double> DnsResolutionDuration = Meter.CreateHistogram<double>(
        DnsResolutionDurationName,
        unit: "s",
        description: "Time taken by the DnsResolver to resolve a host name for the outbound network policy. " +
                     "IP literals are not resolved and not recorded.");

    private static readonly FrozenDictionary<IPAddressScope, string> ScopeNames =
        Enum.GetValues<IPAddressScope>().ToFrozenDictionary(scope => scope, scope => SnakeCase(scope.ToString()));

    public static string ReasonName(OutboundRefusalReason reason) => reason switch {
        OutboundRefusalReason.Port => "port",
        OutboundRefusalReason.BlockedNetwork => "blocked_network",
        _ => "address"
    };

    public static string ScopeName(IPAddressScope scope) => ScopeNames.TryGetValue(scope, out string? name) ? name : "unknown";

    /// <summary>Records a refused connection; <paramref name="scope"/> is absent when no address decided it (a port refusal).</summary>
    public static void RecordRefused(OutboundRefusalReason reason, IPAddressScope? scope) {
        if(!Refused.Enabled) {
            return;
        }

        TagList tags = new() { { ReasonTag, ReasonName(reason) } };
        if(scope is { } known) {
            tags.Add(ScopeTag, ScopeName(known));
        }

        Refused.Add(1, tags);
    }

    public static long StartResolution() => DnsResolutionDuration.Enabled ? Stopwatch.GetTimestamp() : 0;

    public static void RecordResolution(long startTimestamp, string outcome) {
        if(startTimestamp == 0 || !DnsResolutionDuration.Enabled) {
            return;
        }

        DnsResolutionDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, new KeyValuePair<string, object?>(OutcomeTag, outcome));
    }

    private static string SnakeCase(string name) {
        StringBuilder builder = new(name.Length + 4);
        for(int i = 0; i < name.Length; i++) {
            if(char.IsUpper(name[i]) && i > 0) {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[i]));
        }

        return builder.ToString();
    }
}
