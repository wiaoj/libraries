using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;

namespace Wiaoj.Net.Tests.Unit;

/// <summary>
/// A MeterListener sees every measurement of the process, so these tests run alone rather than alongside tests that
/// also connect through a policy.
/// </summary>
[CollectionDefinition(nameof(MetricsCollection), DisableParallelization = true)]
public sealed class MetricsCollection;

/// <summary>Refused connections and DNS resolution time are published on the Wiaoj.Net meter with bounded tags (#142).</summary>
[Collection(nameof(MetricsCollection))]
[Trait("Category", "Unit")]
[Trait("Feature", "Net")]
[Trait("Component", "Metrics")]
public sealed class OutboundNetworkMetricsTests : IDisposable {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly HashSet<string> RefusedTagKeys = ["reason", "scope"];
    private static readonly HashSet<string> Reasons = ["address", "port", "blocked_network"];
    private static readonly HashSet<string> Outcomes = ["success", "failure", "cancelled"];

    private sealed record Measurement(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags);

    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<Measurement> _measurements = new();
    private readonly ConcurrentDictionary<string, Instrument> _instruments = new();

    public OutboundNetworkMetricsTests() {
        this._listener.InstrumentPublished = (instrument, listener) => {
            if(instrument.Meter.Name == "Wiaoj.Net") {
                this._instruments[instrument.Name] = instrument;
                listener.EnableMeasurementEvents(instrument);
            }
        };
        this._listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => this.Record(instrument, value, tags));
        this._listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => this.Record(instrument, value, tags));
        this._listener.Start();
    }

    public void Dispose() => this._listener.Dispose();

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags) {
        Dictionary<string, object?> copy = [];
        foreach(KeyValuePair<string, object?> tag in tags) {
            copy[tag.Key] = tag.Value;
        }

        this._measurements.Enqueue(new Measurement(instrument.Name, value, copy));
    }

    private Measurement[] Refusals() => [.. this._measurements.Where(m => m.Instrument == "wiaoj.net.outbound.refused")];

    private Measurement[] Resolutions() => [.. this._measurements.Where(m => m.Instrument == "wiaoj.net.dns.resolution.duration")];

    private static FakeDnsResolver Resolver() => new(new Dictionary<string, IPAddress[]>(StringComparer.OrdinalIgnoreCase) {
        ["loopback.test"] = [IPAddress.Loopback],
        ["metadata.test"] = [IPAddress.Parse("169.254.169.254"), IPAddress.Parse("10.0.0.5")],
        ["mixed-blocked.test"] = [IPAddress.Parse("10.0.0.5"), IPAddress.Parse("8.8.8.8")]
    });

    private static async Task<OutboundNetworkPolicyException> RefusedAsync(OutboundNetworkPolicy policy, string url, DnsResolver? resolver = null) {
        using HttpClient client = new(new SocketsHttpHandler().UseOutboundNetworkPolicy(policy, resolver ?? Resolver()));
        HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(url, Ct));
        return Assert.IsType<OutboundNetworkPolicyException>(error.InnerException);
    }

    [Fact]
    public void Should_Publish_Both_Instruments_With_Units_And_Descriptions() {
        OutboundNetworkMeter.RecordRefused(OutboundRefusalReason.Port, scope: null);

        Instrument refused = this._instruments["wiaoj.net.outbound.refused"];
        Instrument duration = this._instruments["wiaoj.net.dns.resolution.duration"];

        Assert.IsType<Counter<long>>(refused);
        Assert.Equal("{connection}", refused.Unit);
        Assert.Contains("connections", refused.Description, StringComparison.Ordinal);
        Assert.IsType<Histogram<double>>(duration);
        Assert.Equal("s", duration.Unit);
    }

    [Fact]
    public async Task Should_Count_A_Refused_Address_With_Its_Scope() {
        using LoopbackHttpServer server = new();

        OutboundNetworkPolicyException refusal = await RefusedAsync(OutboundNetworkPolicy.PublicOnly, $"http://loopback.test:{server.Port}/");

        Measurement measurement = Assert.Single(this.Refusals());
        Assert.Equal(1, measurement.Value);
        Assert.Equal("address", measurement.Tags["reason"]);
        Assert.Equal("loopback", measurement.Tags["scope"]);
        Assert.Equal(OutboundRefusalReason.Address, refusal.Reason);
    }

    [Fact]
    public async Task Should_Count_A_Refused_Port_Without_A_Scope() {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.WebOnly;

        await RefusedAsync(policy, "http://loopback.test:6379/");

        Measurement measurement = Assert.Single(this.Refusals());
        Assert.Equal("port", measurement.Tags["reason"]);
        Assert.False(measurement.Tags.ContainsKey("scope"));
    }

    [Fact]
    public async Task Should_Count_A_Blocked_Network_Over_An_Address_Merely_Outside_The_Scopes() {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { BlockedNetworks = [IPNetwork.Parse("10.0.0.0/8")] };

        // 169.254.169.254 is only outside the allowed scopes; 10.0.0.5 hits the explicit rule.
        OutboundNetworkPolicyException refusal = await RefusedAsync(policy, "http://metadata.test/");

        Measurement measurement = Assert.Single(this.Refusals());
        Assert.Equal("blocked_network", measurement.Tags["reason"]);
        Assert.Equal("private", measurement.Tags["scope"]);
        Assert.Equal(OutboundRefusalReason.BlockedNetwork, refusal.Reason);
    }

    [Fact]
    public async Task Should_Report_The_Scope_Of_The_First_Refused_Address_When_None_Is_Blocked() {
        await RefusedAsync(OutboundNetworkPolicy.PublicOnly, "http://metadata.test/");

        Measurement measurement = Assert.Single(this.Refusals());
        Assert.Equal("address", measurement.Tags["reason"]);
        Assert.Equal("link_local", measurement.Tags["scope"]);
    }

    [Fact]
    public async Task Should_Count_Nothing_When_A_Connection_Is_Allowed() {
        using LoopbackHttpServer server = new();
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { AllowedNetworks = [IPNetwork.Parse("127.0.0.0/8")] };
        using HttpClient client = new(new SocketsHttpHandler().UseOutboundNetworkPolicy(policy, Resolver()));

        using HttpResponseMessage response = await client.GetAsync($"http://loopback.test:{server.Port}/", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(this.Refusals());
    }

    [Fact]
    public async Task Should_Not_Count_A_Host_Check_As_A_Refused_Connection() {
        OutboundHostCheck check = await OutboundNetworkPolicy.WebOnly.CheckHostAsync(new Uri("http://metadata.test/"), Resolver(), Ct);
        OutboundHostCheck port = await OutboundNetworkPolicy.WebOnly.CheckHostAsync(new Uri("https://metadata.test:6379/"), Resolver(), Ct);

        Assert.Equal(OutboundHostStatus.Refused, check.Status);
        Assert.Equal(OutboundHostStatus.Refused, port.Status);
        Assert.Empty(this.Refusals());
    }

    [Fact]
    public async Task Should_Report_A_Blocked_Network_From_A_Host_Check() {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { BlockedNetworks = [IPNetwork.Parse("10.0.0.0/8")] };

        OutboundHostCheck refused = await policy.CheckHostAsync("metadata.test", Resolver(), Ct);
        OutboundHostCheck partly = await policy.CheckHostAsync("mixed-blocked.test", Resolver(), Ct);

        Assert.Equal(OutboundRefusalReason.BlockedNetwork, refused.RefusalReason);
        Assert.Equal(OutboundHostStatus.Allowed, partly.Status);
        Assert.Null(partly.RefusalReason);
    }
    [Theory]
    [InlineData("loopback.test", "success")]
    [InlineData("unknown.test", "failure")]
    public async Task Should_Time_A_Resolution_With_Its_Outcome(string host, string outcome) {
        await OutboundNetworkPolicy.PublicOnly.CheckHostAsync(host, Resolver(), Ct);

        Measurement measurement = Assert.Single(this.Resolutions());
        Assert.Equal(outcome, measurement.Tags["outcome"]);
        Assert.Equal(["outcome"], measurement.Tags.Keys);
        Assert.InRange(measurement.Value, 0, 60);
    }

    [Fact]
    public async Task Should_Time_A_Cancelled_Resolution() {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            OutboundNetworkPolicy.PublicOnly.CheckHostAsync("slow.test", new CancellingResolver(), cancelled.Token).AsTask());

        Assert.Equal("cancelled", Assert.Single(this.Resolutions()).Tags["outcome"]);
    }

    [Fact]
    public async Task Should_Time_A_Resolution_On_The_Connection_Path_Too() {
        await RefusedAsync(OutboundNetworkPolicy.PublicOnly, "http://loopback.test/");

        Assert.Equal("success", Assert.Single(this.Resolutions()).Tags["outcome"]);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("[::1]")]
    public async Task Should_Not_Time_An_IP_Literal(string literal) {
        await OutboundNetworkPolicy.PublicOnly.CheckHostAsync(literal, Resolver(), Ct);
        await RefusedAsync(OutboundNetworkPolicy.PublicOnly, $"http://{literal}/");

        Assert.Empty(this.Resolutions());
        Assert.Single(this.Refusals());
    }

    [Fact]
    public async Task Should_Never_Tag_A_Host_Port_Or_Address() {
        using LoopbackHttpServer server = new();
        OutboundNetworkPolicy blocked = OutboundNetworkPolicy.PublicOnly with { BlockedNetworks = [IPNetwork.Parse("10.0.0.0/8")] };

        await RefusedAsync(OutboundNetworkPolicy.PublicOnly, $"http://loopback.test:{server.Port}/");
        await RefusedAsync(OutboundNetworkPolicy.WebOnly, "http://loopback.test:6379/");
        await RefusedAsync(blocked, "http://metadata.test:8080/");
        await RefusedAsync(OutboundNetworkPolicy.PublicOnly, "http://[::ffff:169.254.169.254]:8080/");
        await OutboundNetworkPolicy.PublicOnly.CheckHostAsync("unknown.test", Resolver(), Ct);

        Measurement[] refusals = this.Refusals();
        Assert.Equal(4, refusals.Length);
        Assert.All(refusals, m => {
            Assert.Subset(RefusedTagKeys, m.Tags.Keys.ToHashSet());
            Assert.Contains((string)m.Tags["reason"]!, Reasons);
        });
        Assert.All(this.Resolutions(), m => Assert.Contains((string)m.Tags["outcome"]!, Outcomes));

        string[] values = [.. this._measurements.SelectMany(m => m.Tags.Values).Select(v => v?.ToString() ?? "")];
        string[] scopeNames = [.. Enum.GetValues<IPAddressScope>().Select(s => OutboundNetworkMeter.ScopeName(s))];
        Assert.All(values, value => Assert.True(Reasons.Contains(value) || Outcomes.Contains(value) || scopeNames.Contains(value), value));
    }

    [Fact]
    public void Should_Name_Every_Scope_In_Snake_Case() {
        Assert.Equal("carrier_grade_nat", OutboundNetworkMeter.ScopeName(IPAddressScope.CarrierGradeNat));
        Assert.Equal("link_local", OutboundNetworkMeter.ScopeName(IPAddressScope.LinkLocal));
        Assert.Equal("public", OutboundNetworkMeter.ScopeName(IPAddressScope.Public));
        Assert.Equal(Enum.GetValues<IPAddressScope>().Length, Enum.GetValues<IPAddressScope>().Select(OutboundNetworkMeter.ScopeName).Distinct().Count());
        Assert.All(Enum.GetValues<IPAddressScope>(), scope => Assert.Matches("^[a-z]+(_[a-z]+)*$", OutboundNetworkMeter.ScopeName(scope)));
    }

    private sealed class CancellingResolver : DnsResolver {
        public override async ValueTask<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new SocketException((int)SocketError.HostNotFound);
        }
    }
}
