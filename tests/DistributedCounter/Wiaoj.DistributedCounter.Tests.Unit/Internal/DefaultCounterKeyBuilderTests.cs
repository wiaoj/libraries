using System.Globalization;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.Internal;
using Xunit;

namespace Wiaoj.DistributedCounter.Tests.Unit.Internal;

[Trait("Category", "Unit")]
[Trait("Component", "Internal")]
[Trait("Feature", "CounterKeyBuilder")]
public sealed class DefaultCounterKeyBuilderTests {

    private readonly DefaultCounterKeyBuilder _builder = new();
    private readonly DistributedCounterOptions _defaultOptions = new() {
        GlobalKeyPrefix = "test:prefix:"
    };

    public sealed class TheSimpleBuildMethod {
        private readonly DefaultCounterKeyBuilder _builder = new();
        private readonly DistributedCounterOptions _options = new() { GlobalKeyPrefix = "app:" };

        [Fact]
        public void GivenValidName_PrependsGlobalPrefixCorrectly() {
            // Arrange
            string counterName = "user_logins";

            // Act
            CounterKey result = this._builder.Build(counterName, this._options);

            // Assert
            Assert.Equal("app:user_logins", result.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t\r\n")]
        public void GivenNullOrWhitespaceName_ThrowsArgumentException(string? invalidName) {
            // Arrange & Act & Assert
            Assert.ThrowsAny<ArgumentException>(() => this._builder.Build(invalidName!, this._options));
        }

        [Fact]
        public void GivenEmptyPrefix_BuildsKeyWithJustName() {
            // Arrange
            DistributedCounterOptions optionsWithoutPrefix = new() { GlobalKeyPrefix = "" };

            // Act
            CounterKey result = this._builder.Build("orders", optionsWithoutPrefix);

            // Assert
            Assert.Equal("orders", result.Value);
        }
    }

    public sealed class TheDynamicKeyBuildMethod {
        private readonly DefaultCounterKeyBuilder _builder = new();
        private readonly DistributedCounterOptions _options = new() { GlobalKeyPrefix = "app:" };

        [Fact]
        public void GivenSpanFormattableKey_FormatsEfficiently() {
            // Arrange
            int userId = 12345;

            // Act
            CounterKey result = this._builder.Build("user_rate", userId, this._options);

            // Assert
            Assert.Equal("app:user_rate:12345", result.Value);
        }

        [Fact]
        public void GivenGuidKey_FormatsCorrectly() {
            // Arrange
            Guid guid = Guid.Parse("d3b07384-d113-40e9-a3e9-74d32095f9a6");

            // Act
            CounterKey result = this._builder.Build("sessions", guid, this._options);

            // Assert
            Assert.Equal("app:sessions:d3b07384-d113-40e9-a3e9-74d32095f9a6", result.Value);
        }

        [Fact]
        public void GivenNullKey_FormatsAsNullLiteral() {
            // Arrange
            string? nullKey = null;

            // Act
            CounterKey result = this._builder.Build("requests", nullKey, this._options);

            // Assert
            Assert.Equal("app:requests:null", result.Value);
        }

        [Fact]
        public void GivenLargeSpanFormattableKeyExceeding128Chars_FallsBackToStringFormatting() {
            // Arrange
            LargeFormattableKey largeKey = new(new string('a', 150));

            // Act
            CounterKey result = this._builder.Build("large", largeKey, this._options);

            // Assert
            Assert.Equal($"app:large:{new string('a', 150)}", result.Value);
        }

        private sealed record LargeFormattableKey(string Data) : ISpanFormattable {
            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) {
                if(this.Data.Length > destination.Length) {
                    charsWritten = 0;
                    return false; // Force fallback
                }
                this.Data.AsSpan().CopyTo(destination);
                charsWritten = this.Data.Length;
                return true;
            }

            public string ToString(string? format, IFormatProvider? formatProvider) => this.Data;
        }
    }

    public sealed class TheTagBuildMethod {
        private readonly DefaultCounterKeyBuilder _builder = new();
        private readonly DistributedCounterOptions _options = new() { GlobalKeyPrefix = "counter:" };

        [Fact]
        public void GivenSimpleTagType_AndMatchingName_AvoidsDuplicateTypeNameInKey() {
            // Arrange & Act
            // Name matches typeof(SimpleMarker).Name
            CounterKey result = this._builder.Build<SimpleMarker>("SimpleMarker", this._options);

            // Assert
            Assert.Equal("counter:SimpleMarker", result.Value);
        }

        [Fact]
        public void GivenSimpleTagType_AndCustomName_IncludesBothInKey() {
            // Arrange & Act
            CounterKey result = this._builder.Build<SimpleMarker>("custom_metric", this._options);

            // Assert
            Assert.Equal("counter:SimpleMarker:custom_metric", result.Value);
        }

        [Fact]
        public void GivenGenericTagType_FormatsCleanTypeNameWithoutBacktick() {
            // Arrange & Act
            CounterKey result = this._builder.Build<GenericMarker<string>>("GenericMarker[String]", this._options);

            // Assert
            Assert.Equal("counter:GenericMarker[String]", result.Value);
        }

        [Fact]
        public void GivenMultiGenericTagType_FormatsAllArgumentsCleanly() {
            // Arrange & Act
            CounterKey result = this._builder.Build<MultiGenericMarker<string, int>>("hits", this._options);

            // Assert
            Assert.Equal("counter:MultiGenericMarker[String,Int32]:hits", result.Value);
        }

        [Fact]
        public void GivenNullableTagType_AppendsQuestionMark() {
            // Arrange & Act
            CounterKey result = this._builder.Build<int?>("hits", this._options);

            // Assert
            Assert.Equal("counter:Int32?:hits", result.Value);
        }

        [Fact]
        public void GivenNestedGenericTagType_FormatsNestedHierarchyCorrectly() {
            // Arrange & Act
            CounterKey result = this._builder.Build<GenericMarker<GenericMarker<int>>>("hits", this._options);

            // Assert
            Assert.Equal("counter:GenericMarker[GenericMarker[Int32]]:hits", result.Value);
        }

        [Fact]
        public void GivenTagAndDynamicKey_BuildsCompositeKeyDirectly() {
            // Arrange
            long ipAddressHash = 9876543210L;

            // Act
            CounterKey result = this._builder.Build<SimpleMarker, long>(ipAddressHash, this._options);

            // Assert
            Assert.Equal("counter:SimpleMarker:9876543210", result.Value);
        }

        private sealed class SimpleMarker;
        private sealed class GenericMarker<T>;
        private sealed class MultiGenericMarker<T1, T2>;
    }
}