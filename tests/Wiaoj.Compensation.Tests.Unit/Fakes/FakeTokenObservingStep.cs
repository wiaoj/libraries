using Wiaoj.Compensation;

namespace Wiaoj.Compensation.Tests.Unit.Fakes;

public sealed class FakeTokenObservingStep : ICompensationStep<PipelineTestContext> {
    public string Name { get; }
    private readonly Action<CancellationToken> _tokenObserver;

    public FakeTokenObservingStep(string name, Action<CancellationToken> tokenObserver) {
        this.Name = name;
        this._tokenObserver = tokenObserver;
    }

    public ValueTask ExecuteAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        context.ExecutedSteps.Add(this.Name);
        return ValueTask.CompletedTask;
    }

    public ValueTask CompensateAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        this._tokenObserver(cancellationToken);
        context.CompensatedSteps.Add(this.Name);
        return ValueTask.CompletedTask;
    }
}