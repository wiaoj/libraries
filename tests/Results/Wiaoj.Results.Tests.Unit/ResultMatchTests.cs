using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Match)]
public sealed class ResultMatchTests {

    public sealed class TheMatchMethod {
        [Fact]
        public void Match_WhenSuccess_ExecutesOnValueBranch() {
            // Arrange
            Result<int> result = 42;

            // Act
            string output = result.Match(
                onValue: value => $"val:{value}",
                onError: _ => "err"
            );

            // Assert
            Assert.Equal("val:42", output);
        }

        [Fact]
        public void Match_WhenFailure_ExecutesOnErrorBranch() {
            // Arrange
            Result<int> result = SomeError;

            // Act
            string output = result.Match(
                onValue: _ => "val",
                onError: errors => $"err:{errors.Count}"
            );

            // Assert
            Assert.Equal("err:1", output);
        }
    }

    public sealed class TheSwitchMethod {
        [Fact]
        public void Switch_WhenSuccess_ExecutesOnValueAction() {
            // Arrange
            Result<int> result = 42;
            int capturedValue = 0;

            // Act
            result.Switch(
                onValue: value => { capturedValue = value; },
                onError: _ => { capturedValue = -1; }
            );

            // Assert
            Assert.Equal(42, capturedValue);
        }

        [Fact]
        public void Switch_WhenFailure_ExecutesOnErrorAction() {
            // Arrange
            Result<int> result = SomeError;
            bool errorInvoked = false;

            // Act
            result.Switch(
                onValue: _ => { },
                onError: _ => { errorInvoked = true; }
            );

            // Assert
            Assert.True(errorInvoked);
        }
    }
}