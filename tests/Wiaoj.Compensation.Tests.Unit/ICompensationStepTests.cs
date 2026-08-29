using Wiaoj.Compensation;
using Xunit;

namespace Wiaoj.Compensation.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class ICompensationStepTests {
    private sealed class CustomNamedStep : ICompensationStep<object> {
        public ValueTask ExecuteAsync(object context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    [Fact]
    public void Default_Name_Property_Returns_Class_Type_Name() {
        // Arrange
        ICompensationStep<object> step = new CustomNamedStep();

        // Act
        string stepName = step.Name;

        // Assert
        Assert.Equal(nameof(CustomNamedStep), stepName);
    }

    [Fact]
    public async Task Default_CompensateAsync_Returns_Completed_ValueTask() {
        // Arrange
        ICompensationStep<object> step = new CustomNamedStep();

        // Act
        ValueTask task = step.CompensateAsync(new object(), CancellationToken.None);

        // Assert
        Assert.True(task.IsCompletedSuccessfully);
        await task;
    }
}