using Xunit;

namespace Wiaoj.Resilience.Tests.Unit.Diagnostics;

/// <summary>
/// Serializes the tracing test classes against each other.
/// <para>
/// <see cref="System.Diagnostics.ActivityListener"/> is process-wide: a listener one test registers makes the
/// library start activities for every other test running at that moment. That breaks the test asserting that
/// nothing is started when nobody is subscribed, which needs a window with no listener attached.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ResilienceTracingCollection {
    public const string Name = "resilience-tracing";
}
