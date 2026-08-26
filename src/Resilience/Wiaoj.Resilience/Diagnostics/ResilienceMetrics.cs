using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Wiaoj.Resilience.Diagnostics;

/// <summary>
/// Central metrics provider for circuit breaker operations.
/// </summary>
internal static class ResilienceMetrics {
    public const string MeterName = "Wiaoj.Resilience";

    private static readonly string MeterVersion =
        typeof(ResilienceMetrics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ResilienceMetrics).Assembly.GetName().Version?.ToString()
        ?? "1.0.0";

    public static readonly Meter Meter = new(MeterName, MeterVersion);

    private static readonly ConcurrentDictionary<string, int> CircuitStates = new(StringComparer.Ordinal);

    private static readonly Counter<long> DecisionCounter = Meter.CreateCounter<long>(
        name: "circuitbreaker.decisions",
        unit: "{decision}",
        description: "Number of circuit breaker execution decisions made.");

    private static readonly Counter<long> TripsCounter = Meter.CreateCounter<long>(
        name: "circuitbreaker.trips",
        unit: "{trip}",
        description: "Total number of times a circuit breaker tripped to the open state.");

    private static readonly Counter<long> SuccessCounter = Meter.CreateCounter<long>(
        name: "circuitbreaker.successes",
        unit: "{success}",
        description: "Total number of successful operations recorded.");

    private static readonly Counter<long> FailureCounter = Meter.CreateCounter<long>(
        name: "circuitbreaker.failures",
        unit: "{failure}",
        description: "Total number of failed operations recorded.");

    private static readonly ObservableGauge<int> StateGauge = Meter.CreateObservableGauge(
        name: "circuitbreaker.state",
        observeValues: () => {
            List<Measurement<int>> measurements = new(CircuitStates.Count);
            foreach(var kvp in CircuitStates) {
                measurements.Add(new Measurement<int>(kvp.Value, new KeyValuePair<string, object?>("circuit", kvp.Key)));
            }
            return measurements;
        },
        unit: "{state}",
        description: "Current operational state of the circuit (0=Closed, 1=Open, 2=HalfOpen).");

    public static void RecordDecision(string strategy, string key, CircuitState state, bool isAllowed) {
        CircuitStates[key] = (int)state;

        if(!DecisionCounter.Enabled) {
            return;
        }

        TagList tags = new() {
            { "strategy", strategy },
            { "circuit", key },
            { "state", state.ToString() },
            { "decision", isAllowed ? "allowed" : "denied" }
        };

        DecisionCounter.Add(1, tags);
    }

    public static void RecordTrip(string strategy, string key) {
        CircuitStates[key] = (int)CircuitState.Open;

        if(!TripsCounter.Enabled) {
            return;
        }

        TagList tags = new() {
            { "strategy", strategy },
            { "circuit", key }
        };

        TripsCounter.Add(1, tags);
    }

    public static void RecordSuccess(string strategy, string key, bool wasRecovered) {
        if(wasRecovered) {
            CircuitStates[key] = (int)CircuitState.Closed;
        }

        if(!SuccessCounter.Enabled) {
            return;
        }

        TagList tags = new() {
            { "strategy", strategy },
            { "circuit", key }
        };

        SuccessCounter.Add(1, tags);
    }

    public static void RecordFailure(string strategy, string key) {
        if(!FailureCounter.Enabled) {
            return;
        }

        TagList tags = new() {
            { "strategy", strategy },
            { "circuit", key }
        };

        FailureCounter.Add(1, tags);
    }
}