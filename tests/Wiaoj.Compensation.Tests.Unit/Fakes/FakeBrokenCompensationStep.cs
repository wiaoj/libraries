using Wiaoj.Compensation;

namespace Wiaoj.Compensation.Tests.Unit.Fakes;

public sealed class FakeBrokenCompensationStep : ICompensationStep<PipelineTestContext> {
    public string Name { get; }
    private readonly Exception _compensationException;

    public FakeBrokenCompensationStep(string name = nameof(FakeBrokenCompensationStep), Exception? compensationException = null) {
        this.Name = name;
        this._compensationException = compensationException ?? new TimeoutException("Database connection timed out.");
    }

    public ValueTask ExecuteAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        context.ExecutedSteps.Add(this.Name);
        return ValueTask.CompletedTask;
    }

    public ValueTask CompensateAsync(PipelineTestContext context, CancellationToken cancellationToken) {
        throw this._compensationException;
    }
}