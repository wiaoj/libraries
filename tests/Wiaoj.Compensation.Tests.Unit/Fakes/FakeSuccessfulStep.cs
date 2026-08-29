using Wiaoj.Compensation;

namespace Wiaoj.Compensation.Tests.Unit.Fakes;

public sealed class FakeSuccessfulStep(string name) : ICompensationStep<PipelineTestContext> {
    public string Name { get; } = name;

    public FakeSuccessfulStep() : this (nameof(FakeSuccessfulStep)) { }

    public ValueTask ExecuteAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        context.ExecutedSteps.Add(this.Name);
        return ValueTask.CompletedTask;
    }

    public ValueTask CompensateAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        context.CompensatedSteps.Add(this.Name);
        return ValueTask.CompletedTask;
    }
}