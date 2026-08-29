using Wiaoj.Compensation;
using Xunit;

namespace Wiaoj.Compensation.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class StepCompensationErrorTests {
    [Fact]
    public void Record_Equality_Works_Correctly_For_Identical_Properties() {
        // Arrange
        StepCompensationError error1 = new("Step_1", "Connection failed", "TimeoutException");
        StepCompensationError error2 = new("Step_1", "Connection failed", "TimeoutException");

        // Act & Assert
        Assert.Equal(error1, error2);
        Assert.True(error1 == error2);
    }

    [Fact]
    public void Record_Inequality_Works_For_Different_Properties() {
        // Arrange
        StepCompensationError error1 = new("Step_1", "Connection failed", "TimeoutException");
        StepCompensationError error2 = new("Step_2", "Connection failed", "TimeoutException");

        // Act & Assert
        Assert.NotEqual(error1, error2);
        Assert.True(error1 != error2);
    }
}