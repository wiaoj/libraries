namespace Wiaoj.Compensation;

/// <summary>
/// Defines a reversible unit of work (step) within a compensation pipeline.
/// </summary>
/// <typeparam name="TContext">The shared context type passed across steps.</typeparam>
public interface ICompensationStep<in TContext> {
    /// <summary>
    /// Gets the human-readable identifier of this step for telemetry and reporting.
    /// </summary>
    string Name => this.GetType().Name;

    /// <summary>
    /// Executes the forward action of this step.
    /// </summary>
    /// <param name="context">The shared pipeline context.</param>
    /// <param name="cancellationToken">Cancellation token for forward execution.</param>
    /// <returns>A ValueTask representing the asynchronous execution.</returns>
    ValueTask ExecuteAsync(TContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Reverses (compensates) the side-effects of this step if a subsequent step fails.
    /// </summary>
    /// <param name="context">The shared pipeline context.</param>
    /// <param name="cancellationToken">Dedicated rollback cancellation token with isolated timeout.</param>
    /// <returns>A ValueTask representing the asynchronous rollback.</returns>
    ValueTask CompensateAsync(TContext context, CancellationToken cancellationToken) {
        return ValueTask.CompletedTask;
    }
}