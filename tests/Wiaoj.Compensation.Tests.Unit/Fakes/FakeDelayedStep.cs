using Wiaoj.Compensation;

namespace Wiaoj.Compensation.Tests.Unit.Fakes;

public sealed class FakeDelayedStep : ICompensationStep<PipelineTestContext> {
    public string Name { get; }
    private readonly TimeSpan _delay;

    public FakeDelayedStep(string name, TimeSpan delay) {
        this.Name = name;
        this._delay = delay;
    }

    public async ValueTask ExecuteAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        await Task.Delay(this._delay, cancellationToken);
        context.ExecutedSteps.Add(this.Name);
    }
}