using System.Diagnostics;

namespace Wiaoj.Compensation.Diagnostics;

/// <summary>
/// Central diagnostics and telemetry metadata provider for Wiaoj.Compensation.
/// </summary>
public static class CompensationDiagnostics {
    /// <summary>
    /// The name of the <see cref="ActivitySource"/> used by Wiaoj.Compensation.
    /// </summary>
    public const string ActivitySourceName = "Wiaoj.Compensation";

    /// <summary>
    /// The version of the diagnostic telemetry source.
    /// </summary>
    public static readonly string Version = typeof(CompensationDiagnostics).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// The shared <see cref="ActivitySource"/> instance for OpenTelemetry and APM tracing.
    /// </summary>
    public static readonly ActivitySource Source = new(ActivitySourceName, Version);

    internal static class Tags {
        public const string Status = "compensation.status";
        public const string IsClean = "compensation.is_clean";
        public const string RequiresManualIntervention = "compensation.requires_manual_intervention";
        public const string StepsCount = "compensation.steps_count";
        public const string CompletedStepsCount = "compensation.completed_steps_count";
        public const string CompensatedStepsCount = "compensation.compensated_steps_count";
        public const string FailedStepName = "compensation.failed_step_name";
        public const string ErrorMessage = "compensation.error_message";
    }

    internal static class Activities {
        public const string PipelineRun = "Compensation.Pipeline.Run";
        public const string PipelineRollback = "Compensation.Pipeline.Rollback";
    }
}