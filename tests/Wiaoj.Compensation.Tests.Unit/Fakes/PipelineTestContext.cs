namespace Wiaoj.Compensation.Tests.Unit.Fakes;

public sealed class PipelineTestContext {
    public List<string> ExecutedSteps { get; } = new();
    public List<string> CompensatedSteps { get; } = new();
}