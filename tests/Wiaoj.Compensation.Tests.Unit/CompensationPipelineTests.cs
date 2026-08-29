using Wiaoj.Compensation.Tests.Unit.Fakes;

namespace Wiaoj.Compensation.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class CompensationPipelineTests {
    [Trait("Category", "Unit")]
    public sealed class AddStep {
        [Fact]
        public void Throws_ArgumentNullException_When_Step_Is_Null() {
            // Arrange
            CompensationPipeline<PipelineTestContext> pipeline = new();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => pipeline.AddStep(null!));
        }

        [Fact]
        public void Returns_Same_Pipeline_Instance_For_Fluent_Chaining() {
            // Arrange
            CompensationPipeline<PipelineTestContext> pipeline = new();
            FakeSuccessfulStep step = new();

            // Act
            ICompensationPipeline<PipelineTestContext> chainedPipeline = pipeline.AddStep(step);

            // Assert
            Assert.Same(pipeline, chainedPipeline);
        }
    }

    [Trait("Category", "Unit")]
    public sealed class PipelineOptionsAndDefaults {
        [Fact]
        public async Task Uses_DefaultRollbackTimeout_From_Options_When_No_Timeout_Is_Provided() {
            // Arrange
            CompensationOptions customOptions = new() {
                DefaultRollbackTimeout = TimeSpan.FromMilliseconds(50)
            };

            CompensationPipeline<PipelineTestContext> pipeline = new(customOptions);
            FakeDelayedCompensationStep slowStep = new("Slow_Step", TimeSpan.FromMilliseconds(500));
            FakeFaultyStep faultyStep = new("Faulty_Step");

            pipeline.AddStep(slowStep).AddStep(faultyStep);
            PipelineTestContext context = new();

            // Act - No explicit timeout passed, should use 50ms from customOptions
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(PipelineStatus.Faulted_CompensationTimedOut, report.Status);
            Assert.Single(report.CompensationErrors);
        }

        [Fact]
        public async Task Explicit_RunAsync_Timeout_Overrides_Options_DefaultRollbackTimeout() {
            // Arrange
            CompensationOptions customOptions = new() {
                DefaultRollbackTimeout = TimeSpan.FromSeconds(30) // Long default
            };

            CompensationPipeline<PipelineTestContext> pipeline = new(customOptions);
            FakeDelayedCompensationStep slowStep = new("Slow_Step", TimeSpan.FromMilliseconds(500));
            FakeFaultyStep faultyStep = new("Faulty_Step");

            pipeline.AddStep(slowStep).AddStep(faultyStep);
            PipelineTestContext context = new();

            // Act - Explicit 50ms should override the 30s option
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(
                context,
                rollbackTimeout: TimeSpan.FromMilliseconds(50),
                TestContext.Current.CancellationToken
            );

            // Assert
            Assert.Equal(PipelineStatus.Faulted_CompensationTimedOut, report.Status);
        }
    }

    [Trait("Category", "Unit")]
    public sealed class StepDefaultImplementations {
        [Fact]
        public async Task Default_CompensateAsync_Implementation_Executes_Without_Errors() {
            // Arrange
            PipelineTestContext context = new();
            FakeForwardOnlyStep forwardOnlyStep1 = new("Forward_1");
            FakeFaultyStep faultyStep2 = new("Faulty_2");

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(forwardOnlyStep1)
                .AddStep(faultyStep2);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(report.IsSuccess);
            Assert.True(report.IsClean);
            Assert.Equal(PipelineStatus.Faulted_FullyCompensated, report.Status);
            Assert.Equal(1, report.CompletedStepsCount);
            Assert.Equal(1, report.CompensatedStepsCount); // Default completed task counts as success
            Assert.Empty(report.CompensationErrors);
        }
    }

    [Trait("Category", "Unit")]
    public sealed class PipelineReusability {
        [Fact]
        public async Task Same_Pipeline_Instance_Can_Be_Executed_Multiple_Times_Independently() {
            // Arrange
            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(new FakeSuccessfulStep("Step_1"))
                .AddStep(new FakeSuccessfulStep("Step_2"));

            PipelineTestContext context1 = new();
            PipelineTestContext context2 = new();

            // Act
            CompensationReport<PipelineTestContext> report1 = await pipeline.RunAsync(context1, TestContext.Current.CancellationToken);
            CompensationReport<PipelineTestContext> report2 = await pipeline.RunAsync(context2, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(report1.IsSuccess);
            Assert.True(report2.IsSuccess);
            Assert.Equal(2, context1.ExecutedSteps.Count);
            Assert.Equal(2, context2.ExecutedSteps.Count);
            Assert.Empty(context1.CompensatedSteps);
            Assert.Empty(context2.CompensatedSteps);
        }
    }

    [Trait("Category", "Unit")]
    public sealed class RunAsync_SuccessScenarios {
        [Fact]
        public async Task Returns_Success_When_Pipeline_Has_No_Steps() {
            // Arrange
            CompensationPipeline<PipelineTestContext> pipeline = new();
            PipelineTestContext context = new();

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(report.IsSuccess);
            Assert.True(report.IsClean);
            Assert.False(report.RequiresManualIntervention);
            Assert.Equal(PipelineStatus.Success, report.Status);
            Assert.Equal(0, report.CompletedStepsCount);
            Assert.Equal(0, report.CompensatedStepsCount);
            Assert.Null(report.FailedStepName);
            Assert.Null(report.ErrorMessage);
            Assert.Empty(report.CompensationErrors);
        }

        [Fact]
        public async Task Executes_All_Steps_In_Sequential_Order_And_Does_Not_Compensate() {
            // Arrange
            PipelineTestContext context = new();
            FakeSuccessfulStep step1 = new("Step_1");
            FakeSuccessfulStep step2 = new("Step_2");
            FakeSuccessfulStep step3 = new("Step_3");

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step1)
                .AddStep(step2)
                .AddStep(step3);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(report.IsSuccess);
            Assert.True(report.IsClean);
            Assert.False(report.RequiresManualIntervention);
            Assert.Equal(PipelineStatus.Success, report.Status);
            Assert.Equal(3, report.CompletedStepsCount);
            Assert.Equal(0, report.CompensatedStepsCount);

            Assert.Equal(3, context.ExecutedSteps.Count);
            Assert.Equal("Step_1", context.ExecutedSteps[0]);
            Assert.Equal("Step_2", context.ExecutedSteps[1]);
            Assert.Equal("Step_3", context.ExecutedSteps[2]);

            Assert.Empty(context.CompensatedSteps);
            Assert.Empty(report.CompensationErrors);
        }
    }

    [Trait("Category", "Unit")]
    public sealed class RunAsync_FaultAndRollbackScenarios {
        [Fact]
        public async Task Returns_Faulted_NoCompensationRequired_When_First_Step_Fails() {
            // Arrange
            PipelineTestContext context = new();
            FakeFaultyStep faultyStep = new("First_Step", new InvalidOperationException("First step crashed."));

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(faultyStep);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(report.IsSuccess);
            Assert.True(report.IsClean);
            Assert.False(report.RequiresManualIntervention);
            Assert.Equal(PipelineStatus.Faulted_NoCompensationRequired, report.Status);
            Assert.Equal("First_Step", report.FailedStepName);
            Assert.Equal("First step crashed.", report.ErrorMessage);
            Assert.Equal(0, report.CompletedStepsCount);
            Assert.Equal(0, report.CompensatedStepsCount);
            Assert.Empty(context.ExecutedSteps);
            Assert.Empty(context.CompensatedSteps);
        }

        [Fact]
        public async Task Compensates_Completed_Steps_In_Reverse_Lifo_Order_When_Subsequent_Step_Fails() {
            // Arrange
            PipelineTestContext context = new();
            FakeSuccessfulStep step1 = new("Step_1");
            FakeSuccessfulStep step2 = new("Step_2");
            FakeFaultyStep step3 = new("Step_3", new HttpRequestException("Network failure."));

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step1)
                .AddStep(step2)
                .AddStep(step3);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(report.IsSuccess);
            Assert.True(report.IsClean);
            Assert.False(report.RequiresManualIntervention);
            Assert.Equal(PipelineStatus.Faulted_FullyCompensated, report.Status);
            Assert.Equal("Step_3", report.FailedStepName);
            Assert.Equal("Network failure.", report.ErrorMessage);
            Assert.Equal(2, report.CompletedStepsCount);
            Assert.Equal(2, report.CompensatedStepsCount);
            Assert.Empty(report.CompensationErrors);

            Assert.Equal(2, context.CompensatedSteps.Count);
            Assert.Equal("Step_2", context.CompensatedSteps[0]);
            Assert.Equal("Step_1", context.CompensatedSteps[1]);
        }

        [Fact]
        public async Task Continues_Remaining_Compensations_When_A_Compensation_Step_Throws() {
            // Arrange
            PipelineTestContext context = new();
            FakeSuccessfulStep step1 = new("Step_1");
            FakeBrokenCompensationStep brokenStep2 = new("Broken_Step_2", new TimeoutException("Redis timeout."));
            FakeSuccessfulStep step3 = new("Step_3");
            FakeFaultyStep faultyStep4 = new("Step_4");

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step1)
                .AddStep(brokenStep2)
                .AddStep(step3)
                .AddStep(faultyStep4);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(report.IsSuccess);
            Assert.False(report.IsClean);
            Assert.True(report.RequiresManualIntervention);
            Assert.Equal(PipelineStatus.Faulted_PartiallyCompensated, report.Status);
            Assert.Equal(3, report.CompletedStepsCount);
            Assert.Equal(2, report.CompensatedStepsCount);

            Assert.Contains("Step_3", context.CompensatedSteps);
            Assert.Contains("Step_1", context.CompensatedSteps);

            StepCompensationError item = Assert.Single(report.CompensationErrors);
            Assert.Equal("Broken_Step_2", item.StepName);
            Assert.Equal("Redis timeout.", item.ErrorMessage);
            Assert.Equal(nameof(TimeoutException), item.ExceptionType);
        }
    }

    [Trait("Category", "Unit")]
    public sealed class RunAsync_CancellationAndTimeoutScenarios {
        [Fact]
        public async Task Returns_Cancelled_NoCompensationRequired_When_Token_Is_Already_Cancelled() {
            // Arrange
            PipelineTestContext context = new();
            FakeSuccessfulStep step1 = new("Step_1");
            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>().AddStep(step1);

            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, cancellationToken: cts.Token);

            // Assert
            Assert.False(report.IsSuccess);
            Assert.True(report.IsClean);
            Assert.False(report.RequiresManualIntervention);
            Assert.Equal(PipelineStatus.Cancelled_NoCompensationRequired, report.Status);
            Assert.Empty(context.ExecutedSteps);
            Assert.Empty(context.CompensatedSteps);
        }

        [Fact]
        public async Task Compensates_Completed_Steps_When_Cancellation_Occurs_During_Execution() {
            // Arrange
            PipelineTestContext context = new();
            using CancellationTokenSource cts = new();

            FakeSuccessfulStep step1 = new("Step_1");
            FakeFaultyStep cancellingStep2 = new("Step_2", new OperationCanceledException(cts.Token));

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step1)
                .AddStep(cancellingStep2);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, cancellationToken: cts.Token);

            // Assert
            Assert.False(report.IsSuccess);
            Assert.True(report.IsClean);
            Assert.False(report.RequiresManualIntervention);
            Assert.Equal(PipelineStatus.Cancelled_FullyCompensated, report.Status);
            Assert.Equal(1, report.CompletedStepsCount);
            Assert.Equal(1, report.CompensatedStepsCount);
            Assert.Contains("Step_1", context.CompensatedSteps);
        }

        [Fact]
        public async Task Returns_Cancelled_PartiallyCompensated_When_Cancellation_Occurs_And_Compensation_Throws() {
            // Arrange
            PipelineTestContext context = new();
            using CancellationTokenSource cts = new();

            FakeBrokenCompensationStep brokenStep1 = new("Broken_Step_1");
            FakeFaultyStep cancellingStep2 = new("Step_2", new OperationCanceledException(cts.Token));

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(brokenStep1)
                .AddStep(cancellingStep2);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, cancellationToken: cts.Token);

            // Assert
            Assert.False(report.IsSuccess);
            Assert.False(report.IsClean);
            Assert.True(report.RequiresManualIntervention);
            Assert.Equal(PipelineStatus.Cancelled_PartiallyCompensated, report.Status);
            Assert.Single(report.CompensationErrors);
        }

        [Fact]
        public async Task Returns_Faulted_CompensationTimedOut_When_Rollback_Exceeds_RollbackTimeout() {
            // Arrange
            PipelineTestContext context = new();
            FakeDelayedCompensationStep slowStep = new("Slow_Step", TimeSpan.FromMilliseconds(500));
            FakeFaultyStep faultyStep = new("Faulty_Step");

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(slowStep)
                .AddStep(faultyStep);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(
                context,
                rollbackTimeout: TimeSpan.FromMilliseconds(50),
                TestContext.Current.CancellationToken
            );

            // Assert
            Assert.False(report.IsSuccess);
            Assert.False(report.IsClean);
            Assert.True(report.RequiresManualIntervention);
            Assert.Equal(PipelineStatus.Faulted_CompensationTimedOut, report.Status);
            StepCompensationError item = Assert.Single(report.CompensationErrors);
            Assert.Equal("Slow_Step", item.StepName);
            Assert.Equal(nameof(TimeoutException), report.CompensationErrors[0].ExceptionType);
        }
    }

    [Trait("Category", "Unit")]
    public sealed class RunAsync_HookScenarios {
        [Fact]
        public async Task Fires_OnCompensationFailed_Hook_Immediately_When_Compensation_Fails() {
            // Arrange
            PipelineTestContext context = new();
            List<StepCompensationError> capturedErrors = [];

            FakeBrokenCompensationStep step1 = new("Broken_Step_1", new InvalidOperationException("First rollback crash."));
            FakeBrokenCompensationStep step2 = new("Broken_Step_2", new InvalidOperationException("Second rollback crash."));
            FakeFaultyStep faultyStep3 = new("Trigger_Step");

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step1)
                .AddStep(step2)
                .AddStep(faultyStep3);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(
                context,
                onCompensationFailed: (error) => {
                    capturedErrors.Add(error);
                    return ValueTask.CompletedTask;
                },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, capturedErrors.Count);
            Assert.Equal("Broken_Step_2", capturedErrors[0].StepName);
            Assert.Equal("Broken_Step_1", capturedErrors[1].StepName);
            Assert.Equal(2, report.CompensationErrors.Count);
        }

        [Fact]
        public async Task Does_Not_Break_Pipeline_If_OnCompensationFailed_Hook_Throws_Exception() {
            // Arrange
            PipelineTestContext context = new();
            FakeSuccessfulStep step1 = new("Step_1");
            FakeBrokenCompensationStep brokenStep2 = new("Broken_Step_2");
            FakeFaultyStep faultyStep3 = new("Trigger_Step");

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step1)
                .AddStep(brokenStep2)
                .AddStep(faultyStep3);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(
                context, 
                onCompensationFailed: (_) => throw new Exception("Hook callback threw an unexpected exception!"),
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains("Step_1", context.CompensatedSteps);
            Assert.Equal(PipelineStatus.Faulted_PartiallyCompensated, report.Status);
        }
    }

    [Trait("Category", "Unit")] 
    public sealed class CancellationTokenIsolation {
        [Fact]
        public async Task Rollback_Token_Is_Not_Cancelled_Even_When_Caller_Token_Is_Cancelled() {
            // Arrange
            PipelineTestContext context = new();
            using CancellationTokenSource callerCts = new();

            bool wasRollbackTokenCancelled = true;
            FakeTokenObservingStep step1 = new("Observing_Step", (ct) => {
                wasRollbackTokenCancelled = ct.IsCancellationRequested;
            });

            // Step 2 cancels callerCts AND throws OperationCanceledException during ExecuteAsync
            FakeCancellingStep cancellingStep2 = new("Cancelling_Step", callerCts);

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step1)
                .AddStep(cancellingStep2);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, cancellationToken: callerCts.Token);

            // Assert
            Assert.Equal(PipelineStatus.Cancelled_FullyCompensated, report.Status);
            Assert.False(wasRollbackTokenCancelled, "The rollback cancellation token must remain active and not inherit caller's cancellation!");
        }
    }

    [Trait("Category", "Unit")]
    public sealed class TelemetryAndDurations {
        [Fact]
        public async Task Sets_Positive_Execution_Duration_And_Zero_Rollback_Duration_On_Success() {
            // Arrange
            PipelineTestContext context = new();
            FakeDelayedStep step = new("Delayed_Step", TimeSpan.FromMilliseconds(20));

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(report.IsSuccess);
            Assert.True(report.ExecutionDuration > TimeSpan.Zero);
            Assert.Equal(TimeSpan.Zero, report.RollbackDuration);
            Assert.Same(context, report.Context);
        }

        [Fact]
        public async Task Sets_Positive_Execution_And_Rollback_Durations_On_Failure() {
            // Arrange
            PipelineTestContext context = new();
            FakeDelayedCompensationStep step1 = new("Delayed_Comp_Step", TimeSpan.FromMilliseconds(20));
            FakeFaultyStep step2 = new("Faulty_Step");

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step1)
                .AddStep(step2);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(report.IsSuccess);
            Assert.True(report.ExecutionDuration >= TimeSpan.Zero);
            Assert.True(report.RollbackDuration > TimeSpan.Zero);
        }
    }

    [Trait("Category", "Unit")]
    public sealed class MultiErrorTracking {
        [Fact]
        public async Task Aggregates_Multiple_Distinct_Exception_Types_During_Compensation() {
            // Arrange
            PipelineTestContext context = new();
            FakeBrokenCompensationStep step1 = new("Step_1", new ArgumentException("Invalid argument."));
            FakeBrokenCompensationStep step2 = new("Step_2", new UnauthorizedAccessException("Access denied."));
            FakeBrokenCompensationStep step3 = new("Step_3", new FormatException("Bad format."));
            FakeFaultyStep triggeringStep4 = new("Trigger_Step");

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step1)
                .AddStep(step2)
                .AddStep(step3)
                .AddStep(triggeringStep4);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(PipelineStatus.Faulted_PartiallyCompensated, report.Status);
            Assert.Equal(3, report.CompensationErrors.Count);

            // LIFO order verification for error collection
            Assert.Equal("Step_3", report.CompensationErrors[0].StepName);
            Assert.Equal(nameof(FormatException), report.CompensationErrors[0].ExceptionType);

            Assert.Equal("Step_2", report.CompensationErrors[1].StepName);
            Assert.Equal(nameof(UnauthorizedAccessException), report.CompensationErrors[1].ExceptionType);

            Assert.Equal("Step_1", report.CompensationErrors[2].StepName);
            Assert.Equal(nameof(ArgumentException), report.CompensationErrors[2].ExceptionType);
        }
    }

    [Trait("Category", "Unit")]
    public sealed class DuplicateStepExecution {
        [Fact]
        public async Task Executes_And_Compensates_Same_Step_Instance_Multiple_Times_In_Correct_Lifo_Order() {
            // Arrange
            PipelineTestContext context = new();
            FakeSuccessfulStep reusableStep = new("Reusable_Step");
            FakeFaultyStep faultyStep = new("Faulty_Step");

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(reusableStep)  // 1st time
                .AddStep(reusableStep)  // 2nd time
                .AddStep(faultyStep);   // Trigger failure

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(report.IsSuccess);
            Assert.True(report.IsClean);
            Assert.False(report.RequiresManualIntervention);
            Assert.Equal(PipelineStatus.Faulted_FullyCompensated, report.Status);
            Assert.Equal(2, report.CompletedStepsCount);
            Assert.Equal(2, report.CompensatedStepsCount);

            // Verify forward execution ran twice
            Assert.Equal(2, context.ExecutedSteps.Count);
            Assert.Equal("Reusable_Step", context.ExecutedSteps[0]);
            Assert.Equal("Reusable_Step", context.ExecutedSteps[1]);

            // Verify rollback executed twice in LIFO order
            Assert.Equal(2, context.CompensatedSteps.Count);
            Assert.Equal("Reusable_Step", context.CompensatedSteps[0]);
            Assert.Equal("Reusable_Step", context.CompensatedSteps[1]);
        }

        [Fact]
        public async Task Executes_Same_Step_Type_Added_Via_Generic_Method_Successfully() {
            // Arrange
            PipelineTestContext context = new();

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep<FakeSuccessfulStep>()  // 1st instance created via generic ctor
                .AddStep<FakeSuccessfulStep>(); // 2nd instance created via generic ctor

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(report.IsSuccess);
            Assert.True(report.IsClean);
            Assert.Equal(PipelineStatus.Success, report.Status);
            Assert.Equal(2, report.CompletedStepsCount);
            Assert.Equal(0, report.CompensatedStepsCount);
            Assert.Equal(2, context.ExecutedSteps.Count);
            Assert.Empty(context.CompensatedSteps);
        }
    }

    [Trait("Category", "Unit")]
    public sealed class LambdaStepExecution {
        [Fact]
        public async Task Executes_Inline_Lambda_Steps_And_Compensates_Correctly() {
            // Arrange
            PipelineTestContext context = new();

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(
                    name: "Lambda_Step_1",
                    execute: (ctx, ct) => {
                        ctx.ExecutedSteps.Add("Lambda_Step_1");
                        return ValueTask.CompletedTask;
                    },
                    compensate: (ctx, ct) => {
                        ctx.CompensatedSteps.Add("Lambda_Step_1");
                        return ValueTask.CompletedTask;
                    }
                )
                .AddStep(
                    name: "Lambda_Forward_Only",
                    execute: (ctx, ct) => {
                        ctx.ExecutedSteps.Add("Lambda_Forward_Only");
                        return ValueTask.CompletedTask;
                    }
                )
                .AddStep(
                    name: "Faulty_Trigger",
                    execute: (ctx, ct) => throw new InvalidOperationException("Lambda step faulted.")
                );

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(context, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(report.IsSuccess);
            Assert.True(report.IsClean);
            Assert.Equal(PipelineStatus.Faulted_FullyCompensated, report.Status);
            Assert.Equal(2, report.CompletedStepsCount);
            Assert.Equal(2, report.CompensatedStepsCount);

            Assert.Equal("Lambda_Step_1", context.ExecutedSteps[0]);
            Assert.Equal("Lambda_Forward_Only", context.ExecutedSteps[1]);
            Assert.Equal("Lambda_Step_1", context.CompensatedSteps[0]);
        }

        [Fact]
        public async Task Executes_And_Compensates_Multiple_Lambda_Steps_With_Identical_Names_In_Lifo_Order() {
            // Arrange
            PipelineTestContext context = new();
            List<string> hookCallLogs = [];

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(
                    name: "Duplicate_Lambda_Name",
                    execute: (ctx, ct) => {
                        ctx.ExecutedSteps.Add("First_Instance");
                        return ValueTask.CompletedTask;
                    },
                    compensate: (ctx, ct) => {
                        ctx.CompensatedSteps.Add("First_Instance");
                        return ValueTask.CompletedTask;
                    }
                )
                .AddStep(
                    name: "Duplicate_Lambda_Name",
                    execute: (ctx, ct) => {
                        ctx.ExecutedSteps.Add("Second_Instance");
                        return ValueTask.CompletedTask;
                    },
                    compensate: (ctx, ct) => {
                        ctx.CompensatedSteps.Add("Second_Instance");
                        return ValueTask.CompletedTask;
                    }
                )
                .AddStep(
                    name: "Faulty_Step",
                    execute: (ctx, ct) => throw new InvalidOperationException("Trigger fault.")
                );

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(
                context,
                onCompensationFailed: null,
                onStepCompensated: (stepName, ctx) => {
                    hookCallLogs.Add(stepName);
                    return ValueTask.CompletedTask;
                },
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.False(report.IsSuccess);
            Assert.True(report.IsClean);
            Assert.Equal(PipelineStatus.Faulted_FullyCompensated, report.Status);
            Assert.Equal(2, report.CompletedStepsCount);
            Assert.Equal(2, report.CompensatedStepsCount);

            // Verify forward execution order
            Assert.Equal("First_Instance", context.ExecutedSteps[0]);
            Assert.Equal("Second_Instance", context.ExecutedSteps[1]);

            // Verify LIFO rollback order (Second executed first, First executed second)
            Assert.Equal("Second_Instance", context.CompensatedSteps[0]);
            Assert.Equal("First_Instance", context.CompensatedSteps[1]);

            // Verify hooks were invoked with the identical name twice
            Assert.Equal(2, hookCallLogs.Count);
            Assert.Equal("Duplicate_Lambda_Name", hookCallLogs[0]);
            Assert.Equal("Duplicate_Lambda_Name", hookCallLogs[1]);
        }
    }

    [Trait("Category", "Unit")]
    public sealed class RunAsync_OnStepCompensatedHookScenarios {
        [Fact]
        public async Task Fires_OnStepCompensated_Hook_For_Each_Successfully_Compensated_Step() {
            // Arrange
            PipelineTestContext context = new();
            List<string> compensatedHookLogs = [];

            FakeSuccessfulStep step1 = new("Step_1");
            FakeSuccessfulStep step2 = new("Step_2");
            FakeFaultyStep faultyStep3 = new("Faulty_Step");

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step1)
                .AddStep(step2)
                .AddStep(faultyStep3);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(
                context,
                onCompensationFailed: null,
                onStepCompensated: (stepName, ctx) => {
                    compensatedHookLogs.Add(stepName);
                    return ValueTask.CompletedTask;
                },
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.Equal(PipelineStatus.Faulted_FullyCompensated, report.Status);
            Assert.Equal(2, compensatedHookLogs.Count);
            Assert.Equal("Step_2", compensatedHookLogs[0]);
            Assert.Equal("Step_1", compensatedHookLogs[1]);
        }

        [Fact]
        public async Task Does_Not_Break_Pipeline_If_OnStepCompensated_Hook_Throws_Exception() {
            // Arrange
            PipelineTestContext context = new();
            FakeSuccessfulStep step1 = new("Step_1");
            FakeFaultyStep faultyStep2 = new("Faulty_Step");

            ICompensationPipeline<PipelineTestContext> pipeline = new CompensationPipeline<PipelineTestContext>()
                .AddStep(step1)
                .AddStep(faultyStep2);

            // Act
            CompensationReport<PipelineTestContext> report = await pipeline.RunAsync(
                context,
                onCompensationFailed: null,
                onStepCompensated: (_, _) => throw new Exception("Hook crash simulation!"),
                cancellationToken: TestContext.Current.CancellationToken
            );

            // Assert
            Assert.True(report.IsClean);
            Assert.Equal(PipelineStatus.Faulted_FullyCompensated, report.Status);
            Assert.Contains("Step_1", context.CompensatedSteps);
        }
    }
}