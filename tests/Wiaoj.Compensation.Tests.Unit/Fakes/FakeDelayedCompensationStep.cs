using Wiaoj.Compensation;

namespace Wiaoj.Compensation.Tests.Unit.Fakes;

public sealed class FakeDelayedCompensationStep : ICompensationStep<PipelineTestContext> {
    public string Name { get; }
    private readonly TimeSpan _delay;

    public FakeDelayedCompensationStep(string name = nameof(FakeDelayedCompensationStep), TimeSpan? delay = null) {
        this.Name = name;
        this._delay = delay ?? TimeSpan.FromMilliseconds(500);
    }

    public ValueTask ExecuteAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        context.ExecutedSteps.Add(this.Name);
        return ValueTask.CompletedTask;
    }

    public async ValueTask CompensateAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        await Task.Delay(this._delay, cancellationToken);
        context.CompensatedSteps.Add(this.Name);
    }
}