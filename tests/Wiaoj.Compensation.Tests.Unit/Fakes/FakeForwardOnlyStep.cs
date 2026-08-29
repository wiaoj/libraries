using Wiaoj.Compensation;

namespace Wiaoj.Compensation.Tests.Unit.Fakes;

public sealed class FakeForwardOnlyStep : ICompensationStep<PipelineTestContext> {
    public string Name { get; }

    public FakeForwardOnlyStep(string name = nameof(FakeForwardOnlyStep)) {
        this.Name = name;
    }

    public ValueTask ExecuteAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        context.ExecutedSteps.Add(this.Name);
        return ValueTask.CompletedTask;
    }

    // CompensateAsync is intentionally omitted to verify the default interface method implementation
}