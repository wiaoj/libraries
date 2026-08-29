using System.Diagnostics;
using Wiaoj.Compensation.Diagnostics;
using Wiaoj.Extensions;
using Wiaoj.Preconditions;

namespace Wiaoj.Compensation;

/// <summary>
/// Sequentially executes registered compensation steps with LIFO automatic rollback on failure.
/// </summary>
/// <typeparam name="TContext">The shared context type.</typeparam>
public sealed class CompensationPipeline<TContext> : ICompensationPipeline<TContext> {
    private readonly List<ICompensationStep<TContext>> _steps = [];
    private readonly CompensationOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompensationPipeline{TContext}"/> class with default configuration options.
    /// </summary>
    public CompensationPipeline() : this(new CompensationOptions()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompensationPipeline{TContext}"/> class with custom configuration options.
    /// </summary>
    /// <param name="options">The configuration options for pipeline behavior.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public CompensationPipeline(CompensationOptions options) {
        Preca.ThrowIfNull(options);
        this._options = options;
    }

    /// <inheritdoc />
    public ICompensationPipeline<TContext> AddStep(ICompensationStep<TContext> step) {
        Preca.ThrowIfNull(step);
        this._steps.Add(step);
        return this;
    }

    /// <inheritdoc />
    public ICompensationPipeline<TContext> AddStep<TStep>()
        where TStep : class, ICompensationStep<TContext>, new() {
        return AddStep(new TStep());
    }

    /// <inheritdoc />
    public ICompensationPipeline<TContext> AddStep(
        string name,
        Func<TContext, CancellationToken, ValueTask> execute,
        Func<TContext, CancellationToken, ValueTask> compensate) {
        return AddStep(new DelegateCompensationStep<TContext>(name, execute, compensate));
    }

    /// <inheritdoc />
    public async ValueTask<CompensationReport<TContext>> RunAsync(
        TContext context,
        TimeSpan rollbackTimeout,
        Func<StepCompensationError, ValueTask>? onCompensationFailed,
        Func<string, TContext, ValueTask>? onStepCompensated,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);

        using Activity? activity = CompensationDiagnostics.Source.StartActivity(CompensationDiagnostics.Activities.PipelineRun);
        activity?.SetTag(CompensationDiagnostics.Tags.StepsCount, this._steps.Count);

        long executionStart = Stopwatch.GetTimestamp();
        Stack<ICompensationStep<TContext>> rollbackStack = new(this._steps.Count);

        for(int i = 0; i < this._steps.Count; i++) {
            ICompensationStep<TContext> step = this._steps[i];

            try {
                cancellationToken.ThrowIfCancellationRequested();
                await step.ExecuteAsync(context, cancellationToken);
                rollbackStack.Push(step);
            }
            catch(Exception ex) {
                bool isCancelled = ex is OperationCanceledException;

                CompensationReport<TContext> faultReport = await CompensateAsync(
                    context,
                    rollbackStack,
                    failedStepName: step.Name,
                    rootException: ex,
                    isCancelled: isCancelled,
                    executionDuration: Stopwatch.GetElapsedTime(executionStart),
                    rollbackTimeout: rollbackTimeout.ToPositiveOrDefault(this._options.DefaultRollbackTimeout),
                    onCompensationFailed: onCompensationFailed,
                    onStepCompensated: onStepCompensated
                );

                activity.EnrichWithReport(faultReport);
                return faultReport;
            }
        }


        CompensationReport<TContext> successReport = new(
            status: PipelineStatus.Success,
            context: context,
            failedStepName: null,
            errorMessage: null,
            compensationErrors: [],
            completedStepsCount: this._steps.Count,
            compensatedStepsCount: 0,
            executionDuration: Stopwatch.GetElapsedTime(executionStart),
            rollbackDuration: TimeSpan.Zero
        );

        activity.EnrichWithReport(successReport);
        return successReport;
    }

    private async ValueTask<CompensationReport<TContext>> CompensateAsync(
        TContext context,
        Stack<ICompensationStep<TContext>> rollbackStack,
        string failedStepName,
        Exception rootException,
        bool isCancelled,
        TimeSpan executionDuration,
        TimeSpan rollbackTimeout,
        Func<StepCompensationError, ValueTask>? onCompensationFailed,
        Func<string, TContext, ValueTask>? onStepCompensated) {

        using Activity? rollbackActivity = CompensationDiagnostics.Source.StartActivity(CompensationDiagnostics.Activities.PipelineRollback);

        long rollbackStart = Stopwatch.GetTimestamp();
        int totalToCompensate = rollbackStack.Count;

        rollbackActivity.EnrichWithRollbackStart(failedStepName, totalToCompensate);

        if(totalToCompensate == 0) {
            PipelineStatus noCompStatus = isCancelled
                ? PipelineStatus.Cancelled_NoCompensationRequired
                : PipelineStatus.Faulted_NoCompensationRequired;

            return new CompensationReport<TContext>(
                status: noCompStatus,
                context: context,
                failedStepName: failedStepName,
                errorMessage: rootException.Message,
                compensationErrors: [],
                completedStepsCount: 0,
                compensatedStepsCount: 0,
                executionDuration: executionDuration,
                rollbackDuration: Stopwatch.GetElapsedTime(rollbackStart)
            );
        }

        using CancellationTokenSource rollbackCts = new(rollbackTimeout);
        List<StepCompensationError> compensationErrors = [];
        int successfullyCompensated = 0;
        bool timedOut = false;

        while(rollbackStack.Count > 0) {
            ICompensationStep<TContext> step = rollbackStack.Pop();

            try {
                rollbackCts.Token.ThrowIfCancellationRequested();
                await step.CompensateAsync(context, rollbackCts.Token);
                successfullyCompensated++;

                // Trigger the onStepCompensated hook on successful rollback
                if(onStepCompensated is not null) {
                    try { await onStepCompensated(step.Name, context); } catch { /* Ignore hook crash */ }
                }
            }
            catch(OperationCanceledException) when(rollbackCts.IsCancellationRequested) {
                timedOut = true;
                StepCompensationError error = new(
                    step.Name,
                    "Compensation timed out before step could complete.",
                    nameof(TimeoutException)
                );
                compensationErrors.Add(error);
                await NotifyHookSafelyAsync(onCompensationFailed, error);
            }
            catch(Exception ex) {
                StepCompensationError error = new(
                    step.Name,
                    ex.Message,
                    ex.GetType().Name
                );
                compensationErrors.Add(error);
                await NotifyHookSafelyAsync(onCompensationFailed, error);
            }
        }

        TimeSpan rollbackDuration = Stopwatch.GetElapsedTime(rollbackStart);

        PipelineStatus finalStatus;
        if(timedOut) {
            finalStatus = isCancelled
                ? PipelineStatus.Cancelled_CompensationTimedOut
                : PipelineStatus.Faulted_CompensationTimedOut;
        }
        else if(compensationErrors.Count > 0) {
            finalStatus = isCancelled
                ? PipelineStatus.Cancelled_PartiallyCompensated
                : PipelineStatus.Faulted_PartiallyCompensated;
        }
        else {
            finalStatus = isCancelled
                ? PipelineStatus.Cancelled_FullyCompensated
                : PipelineStatus.Faulted_FullyCompensated;
        }

        CompensationReport<TContext> finalReport = new(
            status: finalStatus,
            context: context,
            failedStepName: failedStepName,
            errorMessage: rootException.Message,
            compensationErrors: compensationErrors,
            completedStepsCount: totalToCompensate,
            compensatedStepsCount: successfullyCompensated,
            executionDuration: executionDuration,
            rollbackDuration: rollbackDuration
        );

        rollbackActivity.EnrichWithReport(finalReport);
        return finalReport;
    }

    private static async ValueTask NotifyHookSafelyAsync(
        Func<StepCompensationError, ValueTask>? hook,
        StepCompensationError error) {
        if(hook is null) return;
        try { await hook(error); } catch { /* Ignore hook failure */ }
    }
}