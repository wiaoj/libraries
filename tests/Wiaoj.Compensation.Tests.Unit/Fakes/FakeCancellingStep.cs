using Wiaoj.Compensation;

namespace Wiaoj.Compensation.Tests.Unit.Fakes;

public sealed class FakeCancellingStep : ICompensationStep<PipelineTestContext> {
    public string Name { get; }
    private readonly CancellationTokenSource _ctsToCancel;

    public FakeCancellingStep(string name, CancellationTokenSource ctsToCancel) {
        this.Name = name;
        this._ctsToCancel = ctsToCancel;
    }

    public ValueTask ExecuteAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        this._ctsToCancel.Cancel();
        throw new OperationCanceledException(this._ctsToCancel.Token);
    }

    public ValueTask CompensateAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        context.CompensatedSteps.Add(this.Name);
        return ValueTask.CompletedTask;
    }
}