using Wiaoj.Preconditions;

namespace Wiaoj.Compensation;

/// <summary>
/// Provides convenient overloads and extension methods for executing and configuring <see cref="ICompensationPipeline{TContext}"/>.
/// </summary>
public static class CompensationPipelineExtensions {
    /// <summary>
    /// Adds a forward-only inline delegate step without a compensation action.
    /// </summary>
    /// <typeparam name="TContext">The shared context type.</typeparam>
    /// <param name="pipeline">The compensation pipeline instance.</param>
    /// <param name="name">The name identifier of the step.</param>
    /// <param name="execute">The forward execution delegate.</param>
    /// <returns>The current pipeline instance for method chaining.</returns>
    public static ICompensationPipeline<TContext> AddStep<TContext>(
        this ICompensationPipeline<TContext> pipeline,
        string name,
        Func<TContext, CancellationToken, ValueTask> execute) {
        Preca.ThrowIfNull(pipeline);
        return pipeline.AddStep(new DelegateCompensationStep<TContext>(name, execute));
    }

    /// <summary>
    /// Runs the pipeline using the configured options rollback timeout and without hooks.
    /// </summary>
    /// <typeparam name="TContext">The shared context type.</typeparam>
    /// <param name="pipeline">The compensation pipeline instance.</param>
    /// <param name="context">The shared pipeline context.</param>
    /// <param name="cancellationToken">Cancellation token for forward execution.</param>
    /// <returns>An immutable report containing final status, telemetry, and error details.</returns>
    public static ValueTask<CompensationReport<TContext>> RunAsync<TContext>(
        this ICompensationPipeline<TContext> pipeline,
        TContext context,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(pipeline);
        Preca.ThrowIfNull(context);

        return pipeline.RunAsync(context, TimeSpan.Zero, null, null, cancellationToken);
    }

    /// <summary>
    /// Runs the pipeline with a custom rollback timeout and without hooks.
    /// </summary>
    /// <typeparam name="TContext">The shared context type.</typeparam>
    /// <param name="pipeline">The compensation pipeline instance.</param>
    /// <param name="context">The shared pipeline context.</param>
    /// <param name="rollbackTimeout">The explicit timeout duration allocated for the compensation phase.</param>
    /// <param name="cancellationToken">Cancellation token for forward execution.</param>
    /// <returns>An immutable report containing final status, telemetry, and error details.</returns>
    public static ValueTask<CompensationReport<TContext>> RunAsync<TContext>(
        this ICompensationPipeline<TContext> pipeline,
        TContext context,
        TimeSpan rollbackTimeout,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(pipeline);
        Preca.ThrowIfNull(context);

        return pipeline.RunAsync(context, rollbackTimeout, null, null, cancellationToken);
    }

    /// <summary>
    /// Runs the pipeline with default rollback timeout and an instant failure hook.
    /// </summary>
    /// <typeparam name="TContext">The shared context type.</typeparam>
    /// <param name="pipeline">The compensation pipeline instance.</param>
    /// <param name="context">The shared pipeline context.</param>
    /// <param name="onCompensationFailed">The instant notification hook fired if a compensation step fails.</param>
    /// <param name="cancellationToken">Cancellation token for forward execution.</param>
    /// <returns>An immutable report containing final status, telemetry, and error details.</returns>
    public static ValueTask<CompensationReport<TContext>> RunAsync<TContext>(
        this ICompensationPipeline<TContext> pipeline,
        TContext context,
        Func<StepCompensationError, ValueTask> onCompensationFailed,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(pipeline);
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(onCompensationFailed);

        return pipeline.RunAsync(context, TimeSpan.Zero, onCompensationFailed, null, cancellationToken);
    }

    /// <summary>
    /// Runs the pipeline with default rollback timeout and both failure and success hooks.
    /// </summary>
    /// <typeparam name="TContext">The shared context type.</typeparam>
    /// <param name="pipeline">The compensation pipeline instance.</param>
    /// <param name="context">The shared pipeline context.</param>
    /// <param name="onCompensationFailed">The instant notification hook fired if a compensation step fails.</param>
    /// <param name="onStepCompensated">The instant notification hook fired when a compensation step succeeds.</param>
    /// <param name="cancellationToken">Cancellation token for forward execution.</param>
    /// <returns>An immutable report containing final status, telemetry, and error details.</returns>
    public static ValueTask<CompensationReport<TContext>> RunAsync<TContext>(
        this ICompensationPipeline<TContext> pipeline,
        TContext context,
        Func<StepCompensationError, ValueTask>? onCompensationFailed,
        Func<string, TContext, ValueTask>? onStepCompensated,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(pipeline);
        Preca.ThrowIfNull(context);

        return pipeline.RunAsync(context, TimeSpan.Zero, onCompensationFailed, onStepCompensated, cancellationToken);
    }
}