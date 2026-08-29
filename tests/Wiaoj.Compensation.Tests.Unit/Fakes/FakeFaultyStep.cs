using Wiaoj.Compensation;

namespace Wiaoj.Compensation.Tests.Unit.Fakes;

public sealed class FakeFaultyStep : ICompensationStep<PipelineTestContext> {
    public string Name { get; }
    private readonly Exception _exceptionToThrow;

    public FakeFaultyStep(string name = nameof(FakeFaultyStep), Exception? exceptionToThrow = null) {
        this.Name = name;
        this._exceptionToThrow = exceptionToThrow ?? new InvalidOperationException("Step execution failed.");
    }

    public ValueTask ExecuteAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        throw this._exceptionToThrow;
    }

    public ValueTask CompensateAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        context.CompensatedSteps.Add(this.Name);
        return ValueTask.CompletedTask;
    }
}