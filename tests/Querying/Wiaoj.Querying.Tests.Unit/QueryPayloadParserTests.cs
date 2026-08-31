using System.Text;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Unit test suite for verifying <see cref="IQueryPayloadParser"/> implementations,
/// media type matching tolerance, parameter stripping, and payload delegation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "PayloadParsers")]
public class QueryPayloadParserTests {
    public sealed class JsonPayloadParserMediaTypeMatching : QueryPayloadParserTests {
        private readonly IQueryPayloadParser _parser = new JsonQueryPayloadParser();

        [Theory]
        [InlineData("application/json")]
        [InlineData("APPLICATION/JSON")]
        [InlineData("Application/Json")]
        [InlineData("application/json; charset=utf-8")]
        [InlineData("application/json;charset=utf-8")]
        [InlineData("application/json ; charset=utf-8; boundary=something")]
        [InlineData("  application/json  ")]
        [InlineData("\tapplication/json\n")]
        public void CanParse_Should_Return_True_For_Valid_Json_MediaTypes(string mediaType) {
            // Act & Assert
            Assert.True(this._parser.CanParse(mediaType));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("text/plain")]
        [InlineData("application/xml")]
        [InlineData("application/x-www-form-urlencoded")]
        [InlineData("application/json-patch+json")]
        [InlineData("application/json-seq")]
        [InlineData("application/vnd.api+json")]
        [InlineData("multipart/form-data")]
        public void CanParse_Should_Return_False_For_Non_Json_MediaTypes(string? mediaType) {
            // Act & Assert
            Assert.False(this._parser.CanParse(mediaType!));
        }
    }

    public sealed class JsonPayloadParserExecution : QueryPayloadParserTests {
        private readonly IQueryPayloadParser _parser = new JsonQueryPayloadParser();

        [Fact]
        public void TryParse_Should_Delegate_And_Parse_Valid_Payload() {
            // Arrange
            byte[] utf8Bytes = Encoding.UTF8.GetBytes("""
            {
              "q": "laptop",
              "sort": "-price",
              "filters": [
                { "field": "price", "op": "gte", "value": 1000 }
              ]
            }
            """);

            // Act
            bool parsed = this._parser.TryParse(utf8Bytes, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal("laptop", result.Q.Value);
            Assert.Equal("-price", result.Sort.ToString());
            var filter = Assert.Single(result.Filters);
            Assert.Equal("price", filter.Field);
            Assert.Equal(QueryOperator.GreaterThanOrEqual, filter.Operator);
            Assert.Equal("1000", filter.RawValue);
        }

        [Theory]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("not_json")]
        [InlineData("{\"filters\": \"invalid\"}")]
        public void TryParse_Should_Return_False_And_Never_Throw_On_Invalid_Payloads(string invalidPayload) {
            // Arrange
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(invalidPayload);

            // Act
            bool parsed = this._parser.TryParse(utf8Bytes, out QueryRequest result);

            // Assert
            Assert.False(parsed);
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void TryParse_Should_Return_False_For_Empty_Buffer() {
            // Act
            bool parsed = this._parser.TryParse(ReadOnlySpan<byte>.Empty, out QueryRequest result);

            // Assert
            Assert.False(parsed);
            Assert.True(result.IsEmpty);
        }
    }

    public sealed class BracketPayloadParserMediaTypeMatching : QueryPayloadParserTests {
        private readonly IQueryPayloadParser _parser = new BracketQueryPayloadParser();

        [Theory]
        [InlineData("text/plain")]
        [InlineData("TEXT/PLAIN")]
        [InlineData("Text/Plain")]
        [InlineData("text/plain; charset=utf-8")]
        [InlineData("text/plain ; charset=utf-8")]
        [InlineData("  text/plain  ")]
        [InlineData("application/x-www-form-urlencoded")]
        [InlineData("APPLICATION/X-WWW-FORM-URLENCODED")]
        [InlineData("application/x-www-form-urlencoded; charset=utf-8")]
        [InlineData("  application/x-www-form-urlencoded  ")]
        public void CanParse_Should_Return_True_For_Text_And_Form_MediaTypes(string mediaType) {
            // Act & Assert
            Assert.True(this._parser.CanParse(mediaType));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("application/json")]
        [InlineData("application/xml")]
        [InlineData("text/html")]
        [InlineData("text/csv")]
        [InlineData("multipart/form-data")]
        public void CanParse_Should_Return_False_For_Other_MediaTypes(string? mediaType) {
            // Act & Assert
            Assert.False(this._parser.CanParse(mediaType!));
        }
    }

    public sealed class BracketPayloadParserExecution : QueryPayloadParserTests {
        private readonly IQueryPayloadParser _parser = new BracketQueryPayloadParser();

        [Fact]
        public void TryParse_Should_Delegate_And_Parse_Valid_Payload() {
            // Arrange
            byte[] utf8Bytes = Encoding.UTF8.GetBytes("q=monitor&sort=price&category[eq]=Electronics");

            // Act
            bool parsed = this._parser.TryParse(utf8Bytes, out QueryRequest result);

            // Assert
            Assert.True(parsed);
            Assert.Equal("monitor", result.Q.Value);
            Assert.Equal("price", result.Sort.ToString());
            var filter = Assert.Single(result.Filters);
            Assert.Equal("category", filter.Field);
            Assert.Equal("Electronics", filter.RawValue);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("=")]
        [InlineData("price[=")]
        public void TryParse_Should_Return_False_And_Never_Throw_On_Invalid_Payloads(string invalidPayload) {
            // Arrange
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(invalidPayload);

            // Act
            bool parsed = this._parser.TryParse(utf8Bytes, out QueryRequest result);

            // Assert
            Assert.False(parsed);
            Assert.True(result.IsEmpty);
        }

        [Fact]
        public void TryParse_Should_Return_False_For_Empty_Buffer() {
            // Act
            bool parsed = this._parser.TryParse(ReadOnlySpan<byte>.Empty, out QueryRequest result);

            // Assert
            Assert.False(parsed);
            Assert.True(result.IsEmpty);
        }
    }
}