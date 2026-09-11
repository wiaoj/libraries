using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Wiaoj.Querying.Expressions;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="TypeConverterHelper.TryConvertValue"/> verifying the IParsable&lt;T&gt;
/// and TypeConverter fallback chains introduced for custom value object support.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "TypeConversion")]
public class TypeConverterHelperFallbackTests {

    #region Test types — IParsable<T>

    /// <summary>
    /// A value object implementing <c>IParsable&lt;T&gt;</c> with the full
    /// <c>TryParse(string?, IFormatProvider?, out T)</c> pattern.
    /// </summary>
    private readonly record struct ParsableVo : IParsable<ParsableVo> {
        public string Value { get; }
        private ParsableVo(string value) => Value = value;

        public static ParsableVo Parse(string s, IFormatProvider? provider) =>
            TryParse(s, provider, out var result)
                ? result
                : throw new FormatException($"Invalid ParsableVo: '{s}'");

        public static bool TryParse(
            [NotNullWhen(true)] string? s,
            IFormatProvider? provider,
            [MaybeNullWhen(false)] out ParsableVo result) {
            if(!string.IsNullOrWhiteSpace(s)) {
                result = new ParsableVo(s.Trim().ToLowerInvariant());
                return true;
            }
            result = default;
            return false;
        }
    }

    /// <summary>
    /// A value object with only a simple static <c>TryParse(string?, out T)</c> method
    /// (no <c>IFormatProvider</c> parameter).
    /// </summary>
    private readonly record struct SimpleParsableVo {
        public string Value { get; }
        private SimpleParsableVo(string value) => Value = value;

        public static bool TryParse([NotNullWhen(true)] string? s, [MaybeNullWhen(false)] out SimpleParsableVo result) {
            if(!string.IsNullOrWhiteSpace(s)) {
                result = new SimpleParsableVo(s.Trim());
                return true;
            }
            result = default;
            return false;
        }
    }

    /// <summary>
    /// A value object whose <c>TryParse</c> rejects specific values to test failure paths.
    /// Only accepts values starting with "valid-".
    /// </summary>
    private readonly record struct StrictParsableVo : IParsable<StrictParsableVo> {
        public string Value { get; }
        private StrictParsableVo(string value) => Value = value;

        public static StrictParsableVo Parse(string s, IFormatProvider? provider) =>
            TryParse(s, provider, out var result)
                ? result
                : throw new FormatException($"Invalid StrictParsableVo: '{s}'");

        public static bool TryParse(
            [NotNullWhen(true)] string? s,
            IFormatProvider? provider,
            [MaybeNullWhen(false)] out StrictParsableVo result) {
            if(s is not null && s.StartsWith("valid-", StringComparison.OrdinalIgnoreCase)) {
                result = new StrictParsableVo(s);
                return true;
            }
            result = default;
            return false;
        }
    }

    #endregion

    #region Test types — TypeConverter

    /// <summary>
    /// A value object that has NO <c>TryParse</c> method but is decorated with a
    /// <see cref="TypeConverterAttribute"/> pointing to <see cref="ConverterOnlyVoConverter"/>.
    /// </summary>
    [TypeConverter(typeof(ConverterOnlyVoConverter))]
    private readonly record struct ConverterOnlyVo {
        public string Value { get; }
        internal ConverterOnlyVo(string value) => Value = value;
    }

    private sealed class ConverterOnlyVoConverter : TypeConverter {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) {
            if(value is string s && !string.IsNullOrWhiteSpace(s)) {
                return new ConverterOnlyVo(s.Trim());
            }
            throw new FormatException("Invalid ConverterOnlyVo value.");
        }
    }

    /// <summary>
    /// A value object with a <see cref="TypeConverterAttribute"/> whose converter rejects
    /// certain inputs to test failure paths.
    /// </summary>
    [TypeConverter(typeof(StrictConverterVoConverter))]
    private readonly record struct StrictConverterVo {
        public string Value { get; }
        internal StrictConverterVo(string value) => Value = value;
    }

    private sealed class StrictConverterVoConverter : TypeConverter {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) {
            if(value is string s && s.StartsWith("ok-", StringComparison.OrdinalIgnoreCase)) {
                return new StrictConverterVo(s);
            }
            throw new FormatException("Value must start with 'ok-'.");
        }
    }

    /// <summary>
    /// A plain struct with no <c>TryParse</c> and no <see cref="TypeConverterAttribute"/>.
    /// Should always fail conversion.
    /// </summary>
    private readonly record struct UnconvertibleVo {
        public int Value { get; }
    }

    #endregion

    public sealed class IParsableFallback : TypeConverterHelperFallbackTests {
        [Fact]
        public void Should_Convert_Via_IParsable_TryParse_With_FormatProvider() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue("hello", typeof(ParsableVo), out object? result);

            // Assert
            Assert.True(success);
            Assert.IsType<ParsableVo>(result);
            Assert.Equal("hello", ((ParsableVo)result).Value);
        }

        [Fact]
        public void Should_Normalize_Value_Via_IParsable() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue("  UPPER  ", typeof(ParsableVo), out object? result);

            // Assert
            Assert.True(success);
            Assert.Equal("upper", ((ParsableVo)result!).Value);
        }

        [Fact]
        public void Should_Fail_When_IParsable_TryParse_Returns_False() {
            // Arrange & Act — empty string is rejected by ParsableVo
            bool success = TypeConverterHelper.TryConvertValue("   ", typeof(ParsableVo), out object? result);

            // Assert
            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void Should_Fail_When_StrictParsable_Rejects_Value() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue("invalid-value", typeof(StrictParsableVo), out object? result);

            // Assert
            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void Should_Succeed_When_StrictParsable_Accepts_Value() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue("valid-item", typeof(StrictParsableVo), out object? result);

            // Assert
            Assert.True(success);
            Assert.IsType<StrictParsableVo>(result);
            Assert.Equal("valid-item", ((StrictParsableVo)result).Value);
        }

        [Fact]
        public void Should_Convert_Via_Simple_TryParse_Without_FormatProvider() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue("world", typeof(SimpleParsableVo), out object? result);

            // Assert
            Assert.True(success);
            Assert.IsType<SimpleParsableVo>(result);
            Assert.Equal("world", ((SimpleParsableVo)result).Value);
        }

        [Fact]
        public void Should_Convert_Nullable_IParsable_Type() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue("test", typeof(ParsableVo?), out object? result);

            // Assert
            Assert.True(success);
            Assert.IsType<ParsableVo>(result);
            Assert.Equal("test", ((ParsableVo)result).Value);
        }
    }

    public sealed class TypeConverterFallback : TypeConverterHelperFallbackTests {
        [Fact]
        public void Should_Convert_Via_TypeConverter_When_No_TryParse_Exists() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue("my-value", typeof(ConverterOnlyVo), out object? result);

            // Assert
            Assert.True(success);
            Assert.IsType<ConverterOnlyVo>(result);
            Assert.Equal("my-value", ((ConverterOnlyVo)result).Value);
        }

        [Fact]
        public void Should_Trim_Value_Via_TypeConverter() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue("  padded  ", typeof(ConverterOnlyVo), out object? result);

            // Assert
            Assert.True(success);
            Assert.Equal("padded", ((ConverterOnlyVo)result!).Value);
        }

        [Fact]
        public void Should_Fail_When_TypeConverter_Throws() {
            // Arrange & Act — StrictConverterVo rejects values not starting with "ok-"
            bool success = TypeConverterHelper.TryConvertValue("bad-value", typeof(StrictConverterVo), out object? result);

            // Assert
            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void Should_Succeed_When_TypeConverter_Accepts_Value() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue("ok-good", typeof(StrictConverterVo), out object? result);

            // Assert
            Assert.True(success);
            Assert.IsType<StrictConverterVo>(result);
            Assert.Equal("ok-good", ((StrictConverterVo)result).Value);
        }

        [Fact]
        public void Should_Fail_When_TypeConverter_Receives_Whitespace() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue("   ", typeof(ConverterOnlyVo), out object? result);

            // Assert
            Assert.False(success);
            Assert.Null(result);
        }
    }

    public sealed class FallbackChainOrdering : TypeConverterHelperFallbackTests {
        [Fact]
        public void Should_Fail_For_Type_Without_TryParse_Or_TypeConverter() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue("42", typeof(UnconvertibleVo), out object? result);

            // Assert
            Assert.False(success);
            Assert.Null(result);
        }

        [Fact]
        public void Should_Still_Handle_Primitives_Before_Fallbacks() {
            // Arrange & Act — int is a primitive; should be handled by the fast path, not fallbacks
            bool success = TypeConverterHelper.TryConvertValue("42", typeof(int), out object? result);

            // Assert
            Assert.True(success);
            Assert.Equal(42, result);
        }

        [Fact]
        public void Should_Return_False_For_Null_Input() {
            // Arrange & Act
            bool success = TypeConverterHelper.TryConvertValue(null, typeof(ParsableVo), out object? result);

            // Assert
            Assert.False(success);
            Assert.Null(result);
        }
    }

    public sealed class SchemaIntegration : TypeConverterHelperFallbackTests {
        private sealed class Entity {
            public ParsableVo Tag { get; set; }
            public ConverterOnlyVo Label { get; set; }
        }

        [Fact]
        public void Should_Validate_Filter_On_IParsable_Property_Without_Custom_Parser() {
            // Arrange
            var schema = new QuerySchema<Entity>()
                .AllowFilter(e => e.Tag);

            var request = new QueryRequest(filters: [
                FilterConditionNode.Equal("Tag", "my-tag")
            ]);

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Should_Validate_Filter_On_TypeConverter_Property_Without_Custom_Parser() {
            // Arrange
            var schema = new QuerySchema<Entity>()
                .AllowFilter(e => e.Label);

            var request = new QueryRequest(filters: [
                FilterConditionNode.Equal("Label", "my-label")
            ]);

            // Act
            QueryValidationResult result = schema.Validate(request);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Should_Reject_Invalid_Value_For_IParsable_Property() {
            // Arrange — StrictParsableVo only accepts "valid-*"
            var schema = new QuerySchema<Entity>() {  };
            schema.Property(e => e.Tag).AllowFilter();

            // Using ParsableVo which accepts anything non-empty, so let's use a schema
            // with a stricter type instead
            var strictSchema = new QuerySchema<StrictEntity>()
                .AllowFilter(e => e.Code);

            var request = new QueryRequest(filters: [
                FilterConditionNode.Equal("Code", "bad-value")
            ]);

            // Act
            QueryValidationResult result = strictSchema.Validate(request);

            // Assert
            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal(QueryValidationErrorCode.InvalidValueFormat, error.ErrorCode);
        }

        private sealed class StrictEntity {
            public StrictParsableVo Code { get; set; }
        }
    }
}
