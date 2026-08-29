namespace Wiaoj.Compensation;

/// <summary>
/// The immutable outcome and telemetry report returned after pipeline execution.
/// </summary>
/// <typeparam name="TContext">The shared context type.</typeparam>
public readonly struct CompensationReport<TContext> {
    /// <summary>
    /// Gets the deterministic final status of the pipeline run.
    /// </summary>
    public PipelineStatus Status { get; }

    /// <summary>
    /// Gets the context instance after pipeline execution.
    /// </summary>
    public TContext Context { get; }

    /// <summary>
    /// Gets the name of the step that faulted, or null if execution succeeded.
    /// </summary>
    public string? FailedStepName { get; }

    /// <summary>
    /// Gets the message of the error that triggered the failure, or null if execution succeeded.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the collection of errors encountered during the rollback phase.
    /// </summary>
    public IReadOnlyList<StepCompensationError> CompensationErrors { get; }

    /// <summary>
    /// Gets the number of forward steps that were successfully executed before completion or failure.
    /// </summary>
    public int CompletedStepsCount { get; }

    /// <summary>
    /// Gets the number of steps that were successfully compensated during rollback.
    /// </summary>
    public int CompensatedStepsCount { get; }

    /// <summary>
    /// Gets the total duration of the forward execution phase.
    /// </summary>
    public TimeSpan ExecutionDuration { get; }

    /// <summary>
    /// Gets the total duration of the backward compensation phase.
    /// </summary>
    public TimeSpan RollbackDuration { get; }

    /// <summary>
    /// Gets a value indicating whether the pipeline finished all forward steps successfully.
    /// </summary>
    public bool IsSuccess => this.Status == PipelineStatus.Success;

    /// <summary>
    /// Gets a value indicating whether the pipeline finished cleanly without leaving dangling uncompensated resources.
    /// </summary>
    public bool IsClean => this.Status is PipelineStatus.Success
                                  or PipelineStatus.Faulted_FullyCompensated
                                  or PipelineStatus.Cancelled_FullyCompensated
                                  or PipelineStatus.Faulted_NoCompensationRequired
                                  or PipelineStatus.Cancelled_NoCompensationRequired;

    /// <summary>
    /// Gets a value indicating whether any compensation failed or timed out, requiring manual review or alerting.
    /// </summary>
    public bool RequiresManualIntervention => this.Status is PipelineStatus.Faulted_PartiallyCompensated
                                                    or PipelineStatus.Cancelled_PartiallyCompensated
                                                    or PipelineStatus.Faulted_CompensationTimedOut
                                                    or PipelineStatus.Cancelled_CompensationTimedOut;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompensationReport{TContext}"/> struct.
    /// </summary>
    /// <param name="status">The final status of the pipeline.</param>
    /// <param name="context">The pipeline context.</param>
    /// <param name="failedStepName">The name of the step that failed, if any.</param>
    /// <param name="errorMessage">The error message that triggered the rollback, if any.</param>
    /// <param name="compensationErrors">Errors encountered during rollback.</param>
    /// <param name="completedStepsCount">Number of completed forward steps.</param>
    /// <param name="compensatedStepsCount">Number of successfully compensated steps.</param>
    /// <param name="executionDuration">Duration of the forward execution phase.</param>
    /// <param name="rollbackDuration">Duration of the rollback phase.</param>
    public CompensationReport(
        PipelineStatus status,
        TContext context,
        string? failedStepName,
        string? errorMessage,
        IReadOnlyList<StepCompensationError> compensationErrors,
        int completedStepsCount,
        int compensatedStepsCount,
        TimeSpan executionDuration,
        TimeSpan rollbackDuration) {
        this.Status = status;
        this.Context = context;
        this.FailedStepName = failedStepName;
        this.ErrorMessage = errorMessage;
        this.CompensationErrors = compensationErrors;
        this.CompletedStepsCount = completedStepsCount;
        this.CompensatedStepsCount = compensatedStepsCount;
        this.ExecutionDuration = executionDuration;
        this.RollbackDuration = rollbackDuration;
    }
}