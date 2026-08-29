using System.Diagnostics;
using System.Runtime.CompilerServices;
using Wiaoj.Compensation.Diagnostics;

namespace Wiaoj.Compensation;

/// <summary>
/// Diagnostic extension helpers for enriching <see cref="Activity"/> spans.
/// </summary>
internal static class CompensationDiagnosticsExtensions {
    /// <summary>
    /// Enriches an activity span with the final <see cref="CompensationReport{TContext}"/> telemetry.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnrichWithReport<TContext>(this Activity? activity, in CompensationReport<TContext> report) {
        if(activity is null || !activity.IsAllDataRequested) return;

        activity.SetTag(CompensationDiagnostics.Tags.Status, report.Status.ToString());
        activity.SetTag(CompensationDiagnostics.Tags.IsClean, report.IsClean);
        activity.SetTag(CompensationDiagnostics.Tags.RequiresManualIntervention, report.RequiresManualIntervention);
        activity.SetTag(CompensationDiagnostics.Tags.CompletedStepsCount, report.CompletedStepsCount);
        activity.SetTag(CompensationDiagnostics.Tags.CompensatedStepsCount, report.CompensatedStepsCount);

        if(report.FailedStepName is not null) {
            activity.SetTag(CompensationDiagnostics.Tags.FailedStepName, report.FailedStepName);
        }

        if(report.IsSuccess) {
            activity.SetStatus(ActivityStatusCode.Ok);
        }
        else {
            activity.SetStatus(ActivityStatusCode.Error, report.ErrorMessage);
            if(report.ErrorMessage is not null) {
                activity.SetTag(CompensationDiagnostics.Tags.ErrorMessage, report.ErrorMessage);
            }
        }
    }

    /// <summary>
    /// Enriches a rollback activity span with starting metadata.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnrichWithRollbackStart(this Activity? activity, string failedStepName, int toCompensateCount) {
        if(activity is null || !activity.IsAllDataRequested) return;

        activity.SetTag(CompensationDiagnostics.Tags.FailedStepName, failedStepName);
        activity.SetTag(CompensationDiagnostics.Tags.CompensatedStepsCount, toCompensateCount);
    }
}