namespace Wiaoj.Compensation;

/// <summary>
/// Represents the deterministic outcome state of an executed compensation pipeline.
/// </summary>
public enum PipelineStatus : byte {
    /// <summary>
    /// All forward steps executed successfully without errors.
    /// </summary>
    Success = 0,

    /// <summary>
    /// A step faulted, but no prior steps had compensations to run.
    /// </summary>
    Faulted_NoCompensationRequired = 10,

    /// <summary>
    /// A step faulted, and all executed prior steps were successfully compensated.
    /// </summary>
    Faulted_FullyCompensated = 11,

    /// <summary>
    /// A step faulted, but one or more compensations failed during rollback (Zombie data alert).
    /// </summary>
    Faulted_PartiallyCompensated = 12,

    /// <summary>
    /// A step faulted, and the rollback phase exceeded its allotted timeout window.
    /// </summary>
    Faulted_CompensationTimedOut = 13,

    /// <summary>
    /// Execution was cancelled before any compensatable steps completed.
    /// </summary>
    Cancelled_NoCompensationRequired = 20,

    /// <summary>
    /// Execution was cancelled, and all executed steps were successfully rolled back.
    /// </summary>
    Cancelled_FullyCompensated = 21,

    /// <summary>
    /// Execution was cancelled, but one or more compensations failed during rollback.
    /// </summary>
    Cancelled_PartiallyCompensated = 22,

    /// <summary>
    /// Execution was cancelled, and the rollback phase timed out.
    /// </summary>
    Cancelled_CompensationTimedOut = 23
}