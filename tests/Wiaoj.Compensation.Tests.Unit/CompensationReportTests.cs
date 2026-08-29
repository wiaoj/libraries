namespace Wiaoj.Compensation.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class CompensationReportTests {
    [Theory]
    [InlineData(PipelineStatus.Success, true, false)]
    [InlineData(PipelineStatus.Faulted_NoCompensationRequired, true, false)]
    [InlineData(PipelineStatus.Faulted_FullyCompensated, true, false)]
    [InlineData(PipelineStatus.Cancelled_NoCompensationRequired, true, false)]
    [InlineData(PipelineStatus.Cancelled_FullyCompensated, true, false)]
    [InlineData(PipelineStatus.Faulted_PartiallyCompensated, false, true)]
    [InlineData(PipelineStatus.Faulted_CompensationTimedOut, false, true)]
    [InlineData(PipelineStatus.Cancelled_PartiallyCompensated, false, true)]
    [InlineData(PipelineStatus.Cancelled_CompensationTimedOut, false, true)]
    public void Status_Properties_Reflect_Correct_Boolean_Flags(
        PipelineStatus status,
        bool expectedIsClean,
        bool expectedRequiresManualIntervention) {
        // Arrange & Act
        CompensationReport<string> report = new(
            status: status,
            context: "test_context",
            failedStepName: null,
            errorMessage: null,
            compensationErrors: [],
            completedStepsCount: 0,
            compensatedStepsCount: 0,
            executionDuration: TimeSpan.Zero,
            rollbackDuration: TimeSpan.Zero
        );

        // Assert
        Assert.Equal(expectedIsClean, report.IsClean);
        Assert.Equal(expectedRequiresManualIntervention, report.RequiresManualIntervention);
    }
}