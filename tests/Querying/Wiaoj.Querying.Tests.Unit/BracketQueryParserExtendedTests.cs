using System.Globalization;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.Tests.Unit;
/// <summary>
/// Extended edge-case suite for <see cref="BracketQueryParser"/>: real-world values,
/// Turkish-culture safety, and fuzz/robustness guarantees.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "BracketParser")]
public class BracketQueryParserExtendedTests {

    /// <summary>
    /// Tests for values that legitimately contain '=' characters (connection strings, tokens, etc.),
    /// proving the parser only splits on the FIRST '=' and preserves the rest verbatim.
    /// </summary>
    public sealed class MultipleEqualsSignsInValue : BracketQueryParserExtendedTests {
        [Fact]
        public void Should_Preserve_Connection_String_Style_Value_With_Multiple_Equals_Signs() {
            // Arrange
            const string input = "connectionString=Server=.;Database=x;User=sa;Password=P@ss=word;";

            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal("connectionString", result.Field);
            Assert.Equal(QueryOperator.Equal, result.Operator);
            Assert.Equal("Server=.;Database=x;User=sa;Password=P@ss=word;", result.RawValue);
        }

        [Fact]
        public void Should_Preserve_Multiple_Equals_Signs_In_Bracket_Form_Value() {
            // Arrange
            const string input = "note[eq]=a=b=c";

            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal("note", result.Field);
            Assert.Equal(QueryOperator.Equal, result.Operator);
            Assert.Equal("a=b=c", result.RawValue);
        }

        [Fact]
        public void Should_Preserve_Value_That_Starts_With_An_Equals_Sign() {
            // Arrange
            const string input = "price[gte]==100";

            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal("price", result.Field);
            Assert.Equal(QueryOperator.GreaterThanOrEqual, result.Operator);
            Assert.Equal("=100", result.RawValue);
        }
    }

    /// <summary>
    /// Tests proving that bracket characters appearing inside the VALUE (as opposed to the key)
    /// do not confuse the bracket-structure detection, since only the key segment is inspected.
    /// </summary>
    public sealed class BracketCharactersInsideValue : BracketQueryParserExtendedTests {
        [Theory]
        [InlineData("tag[eq]=a[b]c]d[e", "tag", QueryOperator.Equal, "a[b]c]d[e")]
        [InlineData("regex[eq]=^[A-Z]{3}[0-9]+$", "regex", QueryOperator.Equal, "^[A-Z]{3}[0-9]+$")]
        public void Should_Preserve_Bracket_Characters_Within_The_Value(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }

    /// <summary>
    /// Documents the current (permissive) behavior when a unary operator receives an explicit
    /// value: the value is silently discarded rather than rejected. If stricter validation is
    /// desired later, this test should be updated to expect <c>false</c> instead.
    /// </summary>
    public sealed class UnaryOperatorWithExplicitValue : BracketQueryParserExtendedTests {
        [Theory]
        [InlineData("deletedAt[isNull]=somevalue")]
        [InlineData("deletedAt[isNotNull]=2026-01-01")]
        public void Should_Silently_Discard_Any_Value_Provided_To_A_Unary_Operator(string input) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal("deletedAt", result.Field);
            Assert.Null(result.RawValue);
        }
    }

    /// <summary>
    /// Tests for operator blocks that are present but consist only of whitespace after trimming.
    /// </summary>
    public sealed class WhitespaceOnlyOperatorBlock : BracketQueryParserExtendedTests {
        [Theory]
        [InlineData("price[   ]=100")]
        [InlineData("price[\t]=100")]
        public void Should_Reject_Operator_Block_That_Is_Whitespace_Only(string input) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.False(isParsed);
            Assert.Equal(default, result);
        }
    }

    /// <summary>
    /// Tests for tab/newline whitespace handling around the entire input, beyond plain spaces.
    /// </summary>
    public sealed class NonSpaceWhitespaceResilience : BracketQueryParserExtendedTests {
        [Fact]
        public void Should_Trim_Tabs_And_Newlines_Surrounding_Input() {
            // Arrange
            const string input = "\tprice[gte]=100\n";

            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal("price", result.Field);
            Assert.Equal(QueryOperator.GreaterThanOrEqual, result.Operator);
            Assert.Equal("100", result.RawValue);
        }
    }

    /// <summary>
    /// Tests for surrogate-pair (non-BMP) Unicode values such as emoji, proving UTF-16 span
    /// slicing does not corrupt characters outside the Basic Multilingual Plane.
    /// </summary>
    public sealed class SurrogatePairValues : BracketQueryParserExtendedTests {
        [Fact]
        public void Should_Preserve_Emoji_Surrogate_Pairs_Intact() {
            // Arrange
            const string input = "name[eq]=\U0001F600\U0001F389"; // 😀🎉

            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal("name", result.Field);
            Assert.Equal(QueryOperator.Equal, result.Operator);
            Assert.Equal("\U0001F600\U0001F389", result.RawValue);
        }
    }

    /// <summary>
    /// Tests proving that array-index-style field names (e.g. "tags[0]") are NOT treated as a
    /// literal field name — the bracket segment is always interpreted as an operator token, so
    /// an unrecognized token like "0" causes rejection. This documents a real limitation: field
    /// names cannot themselves contain bracket syntax.
    /// </summary>
    public sealed class FieldNamesCannotContainBracketSyntax : BracketQueryParserExtendedTests {
        [Theory]
        [InlineData("tags[0]=x")]
        [InlineData("items[1]=y")]
        public void Should_Reject_Array_Index_Style_Field_Names(string input) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.False(isParsed);
            Assert.Equal(default, result);
        }
    }

    /// <summary>
    /// Tests for large values to confirm there is no arbitrary length ceiling and no
    /// performance/stack cliff, since the parser is span-based rather than stackalloc-based.
    /// </summary>
    public sealed class LargeValues : BracketQueryParserExtendedTests {
        [Fact]
        public void Should_Parse_Very_Large_Value_Without_Throwing() {
            // Arrange
            string largeValue = new string('x', 10_000);
            string input = $"description[eq]={largeValue}";

            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal("description", result.Field);
            Assert.Equal(largeValue, result.RawValue);
        }
    }

    /// <summary>
    /// Tests proving operator-token matching is genuinely culture-independent (Ordinal), so the
    /// classic "Turkish I" bug (tr-TR: 'i'.ToUpper() != 'I') cannot break operator recognition,
    /// and that field/value casing is preserved verbatim rather than being culturally folded.
    /// </summary>
    public sealed class TurkishCultureSafety : BracketQueryParserExtendedTests {
        [Theory]
        [InlineData("city[IN]=Ankara,Istanbul", "city", QueryOperator.In, "Ankara,Istanbul")]
        [InlineData("city[in]=Ankara,Istanbul", "city", QueryOperator.In, "Ankara,Istanbul")]
        [InlineData("city[iN]=Ankara,Istanbul", "city", QueryOperator.In, "Ankara,Istanbul")]
        [InlineData("email[ENDSWITH]=.com.tr", "email", QueryOperator.EndsWith, ".com.tr")]
        public void Should_Match_Operators_Correctly_Under_Turkish_Culture(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Arrange
            var original = CultureInfo.CurrentCulture;
            var originalUi = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            try {
                // Act
                bool isParsed = BracketQueryParser.TryParse(input, out var result);

                // Assert
                Assert.True(isParsed);
                Assert.Equal(expectedField, result.Field);
                Assert.Equal(expectedOperator, result.Operator);
                Assert.Equal(expectedValue, result.RawValue);
            }
            finally {
                CultureInfo.CurrentCulture = original;
                CultureInfo.CurrentUICulture = originalUi;
            }
        }

        [Fact]
        public void Should_Preserve_Turkish_Field_Name_And_Value_Verbatim_Without_Case_Folding() {
            // Arrange
            var original = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            const string input = "şehir[eq]=İstanbul";

            try {
                // Act
                bool isParsed = BracketQueryParser.TryParse(input, out var result);

                // Assert
                Assert.True(isParsed);
                Assert.Equal("şehir", result.Field);
                Assert.Equal("İstanbul", result.RawValue);
            }
            finally {
                CultureInfo.CurrentCulture = original;
            }
        }
    }

    /// <summary>
    /// Fuzz/robustness test: feeds thousands of random strings (including Turkish characters,
    /// emoji, and structural tokens like brackets/equals) through the parser and asserts that
    /// it NEVER throws — a malformed or hostile query string must only ever yield <c>false</c>,
    /// never an unhandled exception, since this parser sits directly on untrusted HTTP input.
    /// </summary>
    public sealed class FuzzRobustness : BracketQueryParserExtendedTests {
        [Fact]
        public void Should_Never_Throw_On_Random_Or_Malformed_Input() {
            // Arrange
            var random = new Random(42); // fixed seed: deterministic, reproducible failures
            char[] pool = (
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789[]=.,-_ \t\n" +
                "ıİşŞçÇöÖüÜğĞ\U0001F600"
            ).ToCharArray();

            var exceptions = new List<(string Input, Exception Ex)>();

            // Act
            for(int i = 0; i < 20_000; i++) {
                int length = random.Next(0, 60);
                var chars = new char[length];
                for(int j = 0; j < length; j++) {
                    chars[j] = pool[random.Next(pool.Length)];
                }
                string randomInput = new(chars);

                try {
                    _ = BracketQueryParser.TryParse(randomInput, out _);
                }
                catch(Exception ex) {
                    exceptions.Add((randomInput, ex));
                }
            }

            // Assert
            Assert.True(
                exceptions.Count == 0,
                $"{exceptions.Count} random inputs threw an exception. First: " +
                $"input='{exceptions.FirstOrDefault().Input}', " +
                $"{exceptions.FirstOrDefault().Ex?.GetType().Name}: {exceptions.FirstOrDefault().Ex?.Message}");
        }
    }

    /// <summary>
    /// Tests documenting permissive (non-validating) behavior around field-name formatting,
    /// since <see cref="BracketQueryParser"/> intentionally does not validate field name shape —
    /// that responsibility belongs to a downstream schema/whitelist check.
    /// </summary>
    public sealed class PermissiveFieldNameFormatting : BracketQueryParserExtendedTests {
        [Theory]
        [InlineData("customer.[eq]=x", "customer.", QueryOperator.Equal, "x")]
        [InlineData(".customer[eq]=x", ".customer", QueryOperator.Equal, "x")]
        public void Should_Accept_Field_Names_With_Leading_Or_Trailing_Dots(
            string input,
            string expectedField,
            QueryOperator expectedOperator,
            string expectedValue) {
            // Act
            bool isParsed = BracketQueryParser.TryParse(input, out var result);

            // Assert
            Assert.True(isParsed);
            Assert.Equal(expectedField, result.Field);
            Assert.Equal(expectedOperator, result.Operator);
            Assert.Equal(expectedValue, result.RawValue);
        }
    }
}