using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "DeliveryContext")]
public sealed class WebhookDeliveryContextExtensionsTests {

    public sealed class TheGetHeadersMethod {

        [Fact]
        public void GetHeaders_WhenNoHeadersConfigured_ShouldBeTrulyImmutable_AndNotPolluteGlobalState() {
            // Arrange
            WebhookDeliveryContext context1 = WebhookTestFactory.CreateContext();
            WebhookDeliveryContext context2 = WebhookTestFactory.CreateContext();

            IReadOnlyDictionary<string, string> headers1 = context1.GetHeaders();

            // Act: Kötü niyetli veya dikkatsiz bir kodun downcast edip global singleton'ı bozma denemesi
            if(headers1 is IDictionary<string, string> mutableHeaders) {
                try {
                    mutableHeaders["X-Injected-Header"] = "Hacked";
                }
                catch(NotSupportedException) {
                    // Beklenen güvenli davranış: Eğer gerçekten read-only ise NotSupportedException fırlatır!
                }
            }

            // Assert
            IReadOnlyDictionary<string, string> headers2 = context2.GetHeaders();
            Assert.False(headers2.ContainsKey("X-Injected-Header"),
                "CRITICAL SECURITY FLAW: Global static dictionary was mutated across contexts!");
        }
    }

    public sealed class TheAddOrUpdateHeaderMethod {

        [Fact]
        public void AddOrUpdateHeader_WhenHeaderDoesNotExist_AddsInitialValue() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            // Act
            context.AddOrUpdateHeader(WebhookHeaderNames.WebhookHopCount, "1", current => (int.Parse(current) + 1).ToString());

            // Assert
            Assert.Equal("1", context.GetHeader(WebhookHeaderNames.WebhookHopCount));
        }

        [Fact]
        public void AddOrUpdateHeader_WhenHeaderExists_CallsUpdateFactoryWithCurrentValue() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "2");

            // Act
            context.AddOrUpdateHeader(WebhookHeaderNames.WebhookHopCount, "1", current => (int.Parse(current) + 1).ToString());

            // Assert
            Assert.Equal("3", context.GetHeader(WebhookHeaderNames.WebhookHopCount));
        }

        [Fact]
        public void AddOrUpdateHeader_ThrowsPrecaException_WhenArgumentsAreInvalid() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            // Assert
            Assert.ThrowsAny<ArgumentException>(() => context.AddOrUpdateHeader("", "1", val => val));
            Assert.ThrowsAny<ArgumentException>(() => context.AddOrUpdateHeader("X-Header", null!, val => val));
            Assert.ThrowsAny<ArgumentException>(() => context.AddOrUpdateHeader("X-Header", "1", null!));
        }
    }

    public sealed class TheAppendHeaderMethod {

        [Fact]
        public void AppendHeader_WhenHeaderDoesNotExist_SetsValueWithoutSeparator() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            // Act
            context.AppendHeader("X-Trace-Chain", "node-1");

            // Assert
            Assert.Equal("node-1", context.GetHeader("X-Trace-Chain"));
        }

        [Fact]
        public void AppendHeader_WhenHeaderExists_AppendsValueWithDefaultSeparator() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader("X-Trace-Chain", "node-1");

            // Act
            context.AppendHeader("X-Trace-Chain", "node-2");

            // Assert
            Assert.Equal("node-1, node-2", context.GetHeader("X-Trace-Chain"));
        }

        [Fact]
        public void AppendHeader_WhenHeaderExists_AppendsValueWithCustomSeparator() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader("X-Trace-Chain", "node-1");

            // Act
            context.AppendHeader("X-Trace-Chain", "node-2", " -> ");

            // Assert
            Assert.Equal("node-1 -> node-2", context.GetHeader("X-Trace-Chain"));
        }
    }

    public sealed class TheTryAddHeaderMethod {

        [Fact]
        public void TryAddHeader_WhenHeaderDoesNotExist_AddsAndReturnsTrue() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            // Act
            bool added = context.TryAddHeader("X-Custom", "Value1");

            // Assert
            Assert.True(added);
            Assert.Equal("Value1", context.GetHeader("X-Custom"));
        }

        [Fact]
        public void TryAddHeader_WhenHeaderExists_DoesNotOverwriteAndReturnsFalse() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader("X-Custom", "Original");

            // Act
            bool added = context.TryAddHeader("X-Custom", "NewValue");

            // Assert
            Assert.False(added);
            Assert.Equal("Original", context.GetHeader("X-Custom"));
        }
    }

    public sealed class TheGetHeaderGenericMethod {

        [Fact]
        public void GetHeader_WhenHeaderDoesNotExist_ReturnsDefaultValue() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            // Act & Assert
            Assert.Equal(0, context.GetHeader<int>("X-NonExistent"));
            Assert.Equal(42, context.GetHeader<int>("X-NonExistent", defaultValue: 42));
        }

        [Theory]
        [InlineData("1", 1)]
        [InlineData("42", 42)]
        [InlineData("  7  ", 7)]
        [InlineData("0", 0)]
        [InlineData("1, 2, 3", 3)]
        [InlineData("1, 5, 2", 2)]
        [InlineData("invalid, 4, extra", 4)]
        public void GetHeader_WhenHeaderIsValidInteger_ReturnsParsedValue(string headerValue, int expected) {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader("X-Hop-Count", headerValue);

            // Act & Assert
            Assert.Equal(expected, context.GetHeader<int>("X-Hop-Count"));
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("3.14")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("999999999999999999999999999")] // Overflow
        public void GetHeader_WhenHeaderIsMalformed_ReturnsDefaultValueWithoutThrowing(string malformedValue) {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader("X-Hop-Count", malformedValue);

            // Act & Assert
            Assert.Equal(99, context.GetHeader<int>("X-Hop-Count", defaultValue: 99));
        }

        [Fact]
        public void TryGetHeader_WhenHeaderIsValid_ReturnsTrueAndParsedValue() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader("X-Hop-Count", "10");

            // Act
            bool success = context.TryGetHeader<int>("X-Hop-Count", out int parsed);

            // Assert
            Assert.True(success);
            Assert.Equal(10, parsed);
        }

        [Fact]
        public void TryGetHeader_WhenHeaderIsMalformed_ReturnsFalseAndDefault() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader("X-Hop-Count", "invalid_number");

            // Act
            bool success = context.TryGetHeader<int>("X-Hop-Count", out int parsed);

            // Assert
            Assert.False(success);
            Assert.Equal(0, parsed);
        }
    }

    public sealed class TheGetOrAddAndAddOrUpdateItemMethods {

        [Fact]
        public void GetOrAdd_WhenKeyDoesNotExist_ExecutesFactoryAndStoresValue() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            int factoryExecutions = 0;

            // Act
            string result1 = context.GetOrAdd("custom_item", key => {
                factoryExecutions++;
                return "computed_value";
            });

            string result2 = context.GetOrAdd("custom_item", key => {
                factoryExecutions++;
                return "should_not_run";
            });

            // Assert
            Assert.Equal(1, factoryExecutions);
            Assert.Equal("computed_value", result1);
            Assert.Equal("computed_value", result2);
        }

        [Fact]
        public void AddOrUpdate_WhenKeyDoesNotExist_StoresInitialValue() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            // Act
            int count = context.AddOrUpdate("counter", 1, (key, current) => current + 1);

            // Assert
            Assert.Equal(1, count);
        }

        [Fact]
        public void AddOrUpdate_WhenKeyExists_UpdatesExistingValue() {
            // Arrange
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.Items["counter"] = 5;

            // Act
            int count = context.AddOrUpdate("counter", 1, (key, current) => current + 1);

            // Assert
            Assert.Equal(6, count);
        }
    }
}