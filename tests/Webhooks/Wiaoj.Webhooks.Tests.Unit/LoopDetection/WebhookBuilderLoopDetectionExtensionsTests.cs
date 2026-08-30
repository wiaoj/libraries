using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Webhooks.LoopDetection;

namespace Wiaoj.Webhooks.Tests.Unit.LoopDetection;

[Trait("Category", "Unit")]
[Trait("Feature", "LoopDetection")]
[Trait("Component", "DependencyInjection")]
public sealed class WebhookBuilderLoopDetectionExtensionsTests {

    public sealed class TheUseLoopDetectionMethod {

        [Fact]
        public void UseLoopDetection_DefaultOverload_RegistersDefaults() {
            // Arrange
            ServiceCollection services = new();
            IWebhookBuilder builder = services.AddWiaojWebhooks();

            // Act
            builder.UseLoopDetection();
            ServiceProvider sp = services.BuildServiceProvider();

            // Assert
            LoopDetectionOptions options = sp.GetRequiredService<LoopDetectionOptions>();
            Assert.Equal(5, options.MaxHops);
        }

        [Fact]
        public void UseLoopDetection_MaxHopsOverload_SetsMaxHops() {
            // Arrange
            ServiceCollection services = new();
            IWebhookBuilder builder = services.AddWiaojWebhooks();

            // Act
            builder.UseLoopDetection(7);
            ServiceProvider sp = services.BuildServiceProvider();

            // Assert
            LoopDetectionOptions options = sp.GetRequiredService<LoopDetectionOptions>();
            Assert.Equal(7, options.MaxHops);
        }

        [Fact]
        public void UseLoopDetection_OptionsOverload_SetsCustomOptions() {
            // Arrange
            ServiceCollection services = new();
            IWebhookBuilder builder = services.AddWiaojWebhooks();
            LoopDetectionOptions custom = new() { MaxHops = 12, InstanceId = "node-custom" };

            // Act
            builder.UseLoopDetection(custom);
            ServiceProvider sp = services.BuildServiceProvider();

            // Assert
            LoopDetectionOptions options = sp.GetRequiredService<LoopDetectionOptions>();
            Assert.Equal(12, options.MaxHops);
            Assert.Equal("node-custom", options.InstanceId);
        }

        [Fact]
        public void UseLoopDetection_ConfigureActionOverload_AppliesConfiguration() {
            // Arrange
            ServiceCollection services = new();
            IWebhookBuilder builder = services.AddWiaojWebhooks();

            // Act
            builder.UseLoopDetection(options => {
                options.MaxHops = 10;
                options.InstanceId = "custom-test-node";
            });

            ServiceProvider sp = services.BuildServiceProvider();

            // Assert
            LoopDetectionOptions options = sp.GetRequiredService<LoopDetectionOptions>();
            Assert.Equal(10, options.MaxHops);
            Assert.Equal("custom-test-node", options.InstanceId);
        }

        [Fact]
        public void UseLoopDetection_ThrowsPrecaException_WhenArgumentsInvalid() {
            // Arrange
            IWebhookBuilder builder = null!;
            IWebhookBuilder validBuilder = new ServiceCollection().AddWiaojWebhooks();
            Action<LoopDetectionOptions> nullAction = null!;
            LoopDetectionOptions nullOptions = null!;

            // Act & Assert
            Assert.ThrowsAny<ArgumentException>(() => builder.UseLoopDetection());
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => validBuilder.UseLoopDetection(0));
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => validBuilder.UseLoopDetection(-3));
            Assert.ThrowsAny<ArgumentException>(() => validBuilder.UseLoopDetection(nullOptions));
            Assert.ThrowsAny<ArgumentException>(() => validBuilder.UseLoopDetection(nullAction));
        }
    }
}
