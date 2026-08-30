using Wiaoj.Webhooks.LoopDetection;

namespace Wiaoj.Webhooks.Tests.Unit.LoopDetection;

[Trait("Category", "Unit")]
[Trait("Feature", "LoopDetection")]
[Trait("Component", "Options")]
public sealed class LoopDetectionOptionsTests {

    public sealed class TheDefaults {

        [Fact]
        public void Defaults_ShouldBeConfiguredCorrectly() {
            // Arrange & Act
            LoopDetectionOptions options = new();

            // Assert
            Assert.Equal(5, options.MaxHops);
            Assert.Equal(WebhookHeaderNames.WebhookHopCount, options.HopCountHeaderName);
            Assert.Equal(WebhookHeaderNames.WebhookCausalChain, options.CausalChainHeaderName);
            Assert.True(options.TrackCausalChain);
            Assert.Equal(LoopDetectedBehavior.DropAndLog, options.Behavior);
            Assert.False(string.IsNullOrWhiteSpace(options.InstanceId));
        }
    }

    public sealed class TheValidationRules {

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-10)]
        public void MaxHops_WhenZeroOrNegative_ThrowsPrecaException(int invalidHops) {
            // Arrange
            LoopDetectionOptions options = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => options.MaxHops = invalidHops);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void HopCountHeaderName_WhenNullOrWhiteSpace_ThrowsPrecaException(string? invalidName) {
            // Arrange
            LoopDetectionOptions options = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentException>(() => options.HopCountHeaderName = invalidName!);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void CausalChainHeaderName_WhenNullOrWhiteSpace_ThrowsPrecaException(string? invalidName) {
            // Arrange
            LoopDetectionOptions options = new();

            // Act & Assert
            Assert.ThrowsAny<ArgumentException>(() => options.CausalChainHeaderName = invalidName!);
        }
    }
}