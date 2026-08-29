namespace Wiaoj.Compensation;

/// <summary>
/// Contract for an in-process reversible execution pipeline.
/// </summary>
/// <typeparam name="TContext">The shared context type passed across steps.</typeparam>
public interface ICompensationPipeline<TContext> {
    /// <summary>
    /// Adds a typed step instance to the pipeline.
    /// </summary>
    /// <param name="step">The compensation step instance to add.</param>
    /// <returns>The current pipeline instance for method chaining.</returns>
    ICompensationPipeline<TContext> AddStep(ICompensationStep<TContext> step);

    /// <summary>
    /// Instantiates and adds a step with a parameterless constructor to the pipeline.
    /// </summary>
    /// <typeparam name="TStep">The concrete step type implementing <see cref="ICompensationStep{TContext}"/>.</typeparam>
    /// <returns>The current pipeline instance for method chaining.</returns>
    ICompensationPipeline<TContext> AddStep<TStep>()
        where TStep : class, ICompensationStep<TContext>, new();

    /// <summary>
    /// Adds an inline delegate step with both execute and compensate actions to the pipeline.
    /// </summary>
    /// <param name="name">The name identifier of the step.</param>
    /// <param name="execute">The forward execution delegate.</param>
    /// <param name="compensate">The backward compensation delegate.</param>
    /// <returns>The current pipeline instance for method chaining.</returns>
    ICompensationPipeline<TContext> AddStep(
        string name,
        Func<TContext, CancellationToken, ValueTask> execute,
        Func<TContext, CancellationToken, ValueTask> compensate);

    /// <summary>
    /// Runs the pipeline sequentially. If a step faults or cancels, executes rollback in LIFO order.
    /// </summary>
    /// <param name="context">The shared pipeline context.</param>
    /// <param name="rollbackTimeout">The explicit timeout duration allocated for the compensation phase.</param>
    /// <param name="onCompensationFailed">The instant notification hook fired if a compensation step fails.</param>
    /// <param name="onStepCompensated">The instant notification hook fired when a compensation step succeeds.</param>
    /// <param name="cancellationToken">Cancellation token for forward execution.</param>
    /// <returns>An immutable report containing final status, telemetry, and error details.</returns>
    ValueTask<CompensationReport<TContext>> RunAsync(
        TContext context,
        TimeSpan rollbackTimeout,
        Func<StepCompensationError, ValueTask>? onCompensationFailed,
        Func<string, TContext, ValueTask>? onStepCompensated,
        CancellationToken cancellationToken = default);
}