using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System.Net.Mime;
using System.Text;
using Wiaoj.Querying.AspNetCore.Binders;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.AspNetCore.Tests.Unit;

/// <summary>
/// Comprehensive unit test suite for <see cref="QueryRequestBinder"/> validating URL query collection binding,
/// HTTP QUERY/POST request body payloads, media type negotiations, status code exceptions (415, 413, 400),
/// hybrid source precedences, options policies, and custom parser extensibility.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "AspNetCoreBinding")]
public class QueryRequestBinderTests {
    private static DefaultHttpContext CreateContextWithServices(
        Action<QueryOptions>? configureOptions = null,
        Action<IServiceCollection>? configureServices = null) {
        ServiceCollection services = new();

        IQueryingBuilder builder = services.AddQuerying();
        if(configureOptions != null) {
            builder.Configure(configureOptions);
        }

        configureServices?.Invoke(services);
        ServiceProvider provider = services.BuildServiceProvider();

        DefaultHttpContext context = new() {
            RequestServices = provider
        };
        return context;
    }

    private sealed class TestHttpMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; }
    }

    private sealed class YamlQueryPayloadParser : IQueryPayloadParser {
        public bool CanParse(string mediaType) {
            ReadOnlySpan<char> span = mediaType.AsSpan().Trim();
            int semicolon = span.IndexOf(';');
            ReadOnlySpan<char> baseType = (semicolon >= 0 ? span[..semicolon] : span).Trim();
            return baseType.Equals("application/x-yaml", StringComparison.OrdinalIgnoreCase);
        }

        public bool TryParse(ReadOnlySpan<byte> utf8Payload, out QueryRequest result) {
            string text = Encoding.UTF8.GetString(utf8Payload);
            if(text.Contains("q: yaml_term", StringComparison.Ordinal)) {
                result = new QueryRequest(new Q("yaml_term"));
                return true;
            }
            result = QueryRequest.Empty;
            return false;
        }
    }

    #region URL Query String Binding Tests (Original Suite)

    public sealed class BasicAndImplicitBinding : QueryRequestBinderTests {
        [Fact]
        public async Task Should_Bind_Complete_QueryRequest_From_Standard_QueryCollection() {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["q"] = "workstation",
                ["sort"] = "-price,createdAt",
                ["category[eq]"] = "Electronics",
                ["price[gte]"] = "1500"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.False(result.IsEmpty);
            Assert.Equal("workstation", result.Q.Value);
            Assert.Equal("-price,createdAt", result.Sort.ToString());
            Assert.Equal(2, result.Filters.Count);
            Assert.Equal("category", result.Filters[0].Field);
            Assert.Equal(QueryOperator.Equal, result.Filters[0].Operator);
            Assert.Equal("Electronics", result.Filters[0].RawValue);
            Assert.Equal("price", result.Filters[1].Field);
            Assert.Equal(QueryOperator.GreaterThanOrEqual, result.Filters[1].Operator);
            Assert.Equal("1500", result.Filters[1].RawValue);
        }

        [Theory]
        [InlineData("status", "Active", "status", QueryOperator.Equal, "Active")]
        [InlineData("category", "Books", "category", QueryOperator.Equal, "Books")]
        [InlineData("isFeatured", "true", "isFeatured", QueryOperator.Equal, "true")]
        public void Should_Bind_Implicit_Equality_When_Brackets_Are_Omitted(
            string key,
            string value,
            string expectedField,
            QueryOperator expectedOp,
            string expectedValue) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value
            });

            // Act
            QueryRequest result = QueryRequestBinder.BindAsync(context).Result;

            // Assert
            FilterConditionNode filter = Assert.Single(result.Filters);
            Assert.Equal(expectedField, filter.Field);
            Assert.Equal(expectedOp, filter.Operator);
            Assert.Equal(expectedValue, filter.RawValue);
        }

        [Fact]
        public async Task Should_Return_Empty_QueryRequest_When_QueryCollection_Is_Empty() {
            // Arrange
            DefaultHttpContext context = new();

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.True(result.IsEmpty);
            Assert.True(result.Q.IsEmpty);
            Assert.True(result.Sort.IsEmpty);
            Assert.Empty(result.Filters);
        }
    }

    public sealed class EmptyAndExplicitValueHandling : QueryRequestBinderTests {
        [Theory]
        [InlineData("description[eq]", "", "description", QueryOperator.Equal, "")]
        [InlineData("notes", "", "notes", QueryOperator.Equal, "")]
        [InlineData("code[eq]", "   ", "code", QueryOperator.Equal, "")]
        public async Task Should_Parse_Explicit_Empty_Values_As_Empty_String_For_Binary_Operators(
            string key,
            string value,
            string expectedField,
            QueryOperator expectedOp,
            string expectedValue) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            FilterConditionNode filter = Assert.Single(result.Filters);
            Assert.Equal(expectedField, filter.Field);
            Assert.Equal(expectedOp, filter.Operator);
            Assert.Equal(expectedValue, filter.RawValue);
        }

        [Theory]
        [InlineData("deletedAt[isNull]", "")]
        [InlineData("deletedAt[isNull]", null)]
        [InlineData("assignedTo[isNotNull]", "")]
        [InlineData("assignedTo[isNotNull]", null)]
        public async Task Should_Bind_Unary_Null_Operators_With_Null_RawValue(string key, string? value) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value is null ? StringValues.Empty : new StringValues(value)
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            FilterConditionNode filter = Assert.Single(result.Filters);
            Assert.True(filter.IsUnary);
            Assert.Null(filter.RawValue);
        }
    }

    public sealed class CaseInsensitiveParameterBinding : QueryRequestBinderTests {
        [Theory]
        [InlineData("Q", "laptop", "laptop")]
        [InlineData("q", "laptop", "laptop")]
        [InlineData("Sort", "-Price,CreatedAt", "-price,createdAt")]
        [InlineData("SORT", "+PRICE", "price")]
        public async Task Should_Bind_Search_And_Sort_Regardless_Of_Key_Casing(
            string key,
            string value,
            string expectedValue) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            if(key.Equals("q", StringComparison.OrdinalIgnoreCase)) {
                Assert.Equal(expectedValue, result.Q.Value);
            }
            else {
                Assert.Equal(expectedValue, result.Sort.ToString(), ignoreCase: true);
            }
        }

        [Theory]
        [InlineData("price[GTE]", "300", QueryOperator.GreaterThanOrEqual)]
        [InlineData("price[gte]", "300", QueryOperator.GreaterThanOrEqual)]
        [InlineData("status[EQ]", "Active", QueryOperator.Equal)]
        [InlineData("title[CONTAINS]", "Desk", QueryOperator.Contains)]
        [InlineData("title[NOTCONTAINS]", "Chair", QueryOperator.NotContains)]
        [InlineData("sku[STARTSWITH]", "ABC", QueryOperator.StartsWith)]
        [InlineData("sku[NOTSTARTSWITH]", "XYZ", QueryOperator.NotStartsWith)]
        [InlineData("file[ENDSWITH]", ".pdf", QueryOperator.EndsWith)]
        [InlineData("file[NOTENDSWITH]", ".tmp", QueryOperator.NotEndsWith)]
        [InlineData("deletedAt[ISNULL]", "", QueryOperator.IsNull)]
        [InlineData("deletedAt[ISNOTNULL]", "", QueryOperator.IsNotNull)]
        public async Task Should_Bind_Operators_Regardless_Of_Operator_Casing(
            string key,
            string value,
            QueryOperator expectedOp) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            FilterConditionNode filter = Assert.Single(result.Filters);
            Assert.Equal(expectedOp, filter.Operator);
        }
    }

    public sealed class MultiValueAndCompositeKeys : QueryRequestBinderTests {
        [Fact]
        public async Task Should_Bind_Multiple_Values_Under_Same_Field_Key() {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["status[eq]"] = new StringValues(["Active", "Pending", "Archived"])
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal(3, result.Filters.Count);
            Assert.Equal("Active", result.Filters[0].RawValue);
            Assert.Equal("Pending", result.Filters[1].RawValue);
            Assert.Equal("Archived", result.Filters[2].RawValue);
        }

        [Fact]
        public async Task Should_Bind_Multiple_Different_Operators_For_Same_Property() {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["price[gte]"] = "100",
                ["price[lte]"] = "1000"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal(2, result.Filters.Count);
            Assert.Equal("price", result.Filters[0].Field);
            Assert.Equal(QueryOperator.GreaterThanOrEqual, result.Filters[0].Operator);
            Assert.Equal("100", result.Filters[0].RawValue);

            Assert.Equal("price", result.Filters[1].Field);
            Assert.Equal(QueryOperator.LessThanOrEqual, result.Filters[1].Operator);
            Assert.Equal("1000", result.Filters[1].RawValue);
        }

        [Fact]
        public async Task Should_Bind_Deep_Nested_Navigation_Properties() {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["company.department.manager.address.city[eq]"] = "Berlin",
                ["items.product.category.code[startsWith]"] = "TECH"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal(2, result.Filters.Count);
            Assert.Equal("company.department.manager.address.city", result.Filters[0].Field);
            Assert.Equal("Berlin", result.Filters[0].RawValue);
            Assert.Equal("items.product.category.code", result.Filters[1].Field);
            Assert.Equal("TECH", result.Filters[1].RawValue);
        }
    }

    public sealed class SpecialCharactersAndDelimiters : QueryRequestBinderTests {
        [Theory]
        [InlineData("formula[eq]", "x=y+z", "x=y+z")]
        [InlineData("token[eq]", "dGVzdD1kYXRhPT0=", "dGVzdD1kYXRhPT0=")]
        [InlineData("regex[contains]", "[a-z0-9]+", "[a-z0-9]+")]
        [InlineData("range[between]", "-20.5..150.0", "-20.5..150.0")]
        [InlineData("list[in]", "A,B,C,D", "A,B,C,D")]
        [InlineData("city[eq]", "İstanbul", "İstanbul")]
        [InlineData("currency[eq]", "€", "€")]
        public async Task Should_Preserve_Values_With_Equals_Signs_Brackets_And_Unicode(
            string key,
            string value,
            string expectedValue) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [key] = value
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            FilterConditionNode filter = Assert.Single(result.Filters);
            Assert.Equal(expectedValue, filter.RawValue);
        }
    }

    public sealed class MalformedInputResilience : QueryRequestBinderTests {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("price[=")]
        [InlineData("price]100")]
        [InlineData("price[gte=100")]
        [InlineData("price[unknownOp]")]
        [InlineData("[eq]")]
        [InlineData("price[]")]
        [InlineData("price[[gte]]")]
        public async Task Should_Silently_Skip_Malformed_Query_Parameters_Without_Exceptions(string malformedKey) {
            // Arrange
            DefaultHttpContext context = new();
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                [malformedKey] = "some_value",
                ["validField[eq]"] = "ValidValue"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            FilterConditionNode filter = Assert.Single(result.Filters);
            Assert.Equal("validField", filter.Field);
            Assert.Equal("ValidValue", filter.RawValue);
        }
    }

    public sealed class PreconditionEnforcement : QueryRequestBinderTests {
        [Fact]
        public async Task Should_Throw_ArgumentNullException_When_HttpContext_Is_Null() {
            // Act & Assert
            await Assert.ThrowsAnyAsync<ArgumentNullException>(() => QueryRequestBinder.BindAsync(null!).AsTask());
        }
    }

    #endregion

    #region HTTP QUERY / POST Request Body & Status Codes Tests (New Suite)

    public sealed class HttpMethodsAndPayloadRouting : QueryRequestBinderTests {
        [Theory]
        [InlineData("QUERY")]
        [InlineData("POST")]
        public async Task Should_Bind_Json_Payload_From_Body_For_Supported_Http_Methods(string httpMethod) {
            // Arrange
            const string jsonPayload = """
            {
              "q": "workstation",
              "sort": "-price,createdAt",
              "filters": [
                { "field": "category", "op": "eq", "value": "Electronics" },
                { "field": "price", "op": "gte", "value": 1500 }
              ]
            }
            """;

            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = httpMethod;
            context.Request.ContentType = MediaTypeNames.Application.Json;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonPayload));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.False(result.IsEmpty);
            Assert.Equal("workstation", result.Q.Value);
            Assert.Equal("-price,createdAt", result.Sort.ToString());
            Assert.Equal(2, result.Filters.Count);
            Assert.Equal("category", result.Filters[0].Field);
            Assert.Equal("Electronics", result.Filters[0].RawValue);
            Assert.Equal("price", result.Filters[1].Field);
            Assert.Equal(1500m.ToString(), result.Filters[1].RawValue);
        }

        [Fact]
        public async Task Should_Bind_Bracket_Query_From_Text_Plain_Body() {
            // Arrange
            const string plainTextPayload = "q=monitor&sort=price&stock[gt]=5&deletedAt[isNull]";

            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = MediaTypeNames.Text.Plain;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(plainTextPayload));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("monitor", result.Q.Value);
            Assert.Equal("price", result.Sort.ToString());
            Assert.Equal(2, result.Filters.Count);
            Assert.Equal("stock", result.Filters[0].Field);
            Assert.Equal(QueryOperator.GreaterThan, result.Filters[0].Operator);
            Assert.Equal("5", result.Filters[0].RawValue);
            Assert.Equal("deletedAt", result.Filters[1].Field);
            Assert.Equal(QueryOperator.IsNull, result.Filters[1].Operator);
            Assert.Null(result.Filters[1].RawValue);
        }

        [Fact]
        public async Task Should_Bind_FormUrlEncoded_Body_As_Bracket_Query() {
            // Arrange
            const string formPayload = "q=chair&sort=-price&category[eq]=Furniture";

            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "POST";
            context.Request.ContentType = MediaTypeNames.Application.FormUrlEncoded;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(formPayload));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("chair", result.Q.Value);
            Assert.Equal("-price", result.Sort.ToString());
            FilterConditionNode filter = Assert.Single(result.Filters);
            Assert.Equal("category", filter.Field);
            Assert.Equal("Furniture", filter.RawValue);
        }

        [Theory]
        [InlineData("GET")]
        [InlineData("DELETE")]
        [InlineData("HEAD")]
        public async Task Should_Bind_From_Url_QueryString_For_Standard_Read_Methods(string httpMethod) {
            // Arrange
            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = httpMethod;
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["q"] = "keyboard",
                ["sort"] = "-price",
                ["status[eq]"] = "Active"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("keyboard", result.Q.Value);
            Assert.Equal("-price", result.Sort.ToString());
            FilterConditionNode filter = Assert.Single(result.Filters);
            Assert.Equal("status", filter.Field);
            Assert.Equal("Active", filter.RawValue);
        }
    }

    public sealed class ContentTypeToleranceAndCharsetVariations : QueryRequestBinderTests {
        [Theory]
        [InlineData("application/json; charset=utf-8")]
        [InlineData("APPLICATION/JSON; CHARSET=UTF-8")]
        [InlineData("application/json;charset=utf-8")]
        [InlineData("application/json ; charset=utf-8; boundary=something")]
        [InlineData("  application/json  ")]
        [InlineData("\tapplication/json\n")]
        public async Task Should_Parse_Json_Body_With_Various_ContentType_Header_Formats(string contentType) {
            // Arrange
            const string jsonPayload = """{"q": "tablet", "sort": "price"}""";

            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = contentType;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonPayload));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("tablet", result.Q.Value);
            Assert.Equal("price", result.Sort.ToString());
        }

        [Theory]
        [InlineData("text/plain; charset=utf-8")]
        [InlineData("TEXT/PLAIN")]
        [InlineData("application/x-www-form-urlencoded; charset=utf-8")]
        [InlineData("  application/x-www-form-urlencoded  ")]
        public async Task Should_Parse_Text_And_Form_Bodies_With_Various_ContentType_Header_Formats(string contentType) {
            // Arrange
            const string payload = "q=desk&sort=-price";

            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = contentType;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("desk", result.Q.Value);
            Assert.Equal("-price", result.Sort.ToString());
        }
    }

    public sealed class HttpStatusExceptionsAndPayloadGuards : QueryRequestBinderTests {
        [Theory]
        [InlineData("application/xml")]
        [InlineData("application/pdf")]
        [InlineData("application/json-patch+json")]
        [InlineData("multipart/form-data")]
        [InlineData("text/html")]
        public async Task Should_Throw_415_UnsupportedMediaType_When_Body_Has_Unregistered_ContentType(string unsupportedType) {
            // Arrange
            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = unsupportedType;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("sample_payload_content"));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act & Assert
            BadHttpRequestException ex = await Assert.ThrowsAsync<BadHttpRequestException>(() =>
                QueryRequestBinder.BindAsync(context).AsTask());

            Assert.Equal(StatusCodes.Status415UnsupportedMediaType, ex.StatusCode);
        }

        [Fact]
        public async Task Should_Throw_413_PayloadTooLarge_When_Body_Exceeds_Server_MaxRequestBodySize() {
            // Arrange
            const string jsonPayload = """{"q": "excessively_long_search_term"}""";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);

            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = MediaTypeNames.Application.Json;
            context.Request.Body = new MemoryStream(payloadBytes);
            context.Request.ContentLength = payloadBytes.Length;

            TestHttpMaxRequestBodySizeFeature maxBodyFeature = new() { MaxRequestBodySize = 15 };
            context.Features.Set<IHttpMaxRequestBodySizeFeature>(maxBodyFeature);

            // Act & Assert
            BadHttpRequestException ex = await Assert.ThrowsAsync<BadHttpRequestException>(() =>
                QueryRequestBinder.BindAsync(context).AsTask());

            Assert.Equal(StatusCodes.Status413PayloadTooLarge, ex.StatusCode);
        }

        [Theory]
        [InlineData("""{"q": 12345}""")]
        [InlineData("""{"sort": {}}""")]
        [InlineData("""{"filters": "invalid_array"}""")]
        [InlineData("""{"filters": [{"op": "eq"}]}""")]
        [InlineData("""{""")]
        public async Task Should_Throw_400_BadRequest_When_Json_Payload_Syntax_Is_Malformed(string malformedJson) {
            // Arrange
            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = MediaTypeNames.Application.Json;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(malformedJson));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act & Assert
            BadHttpRequestException ex = await Assert.ThrowsAsync<BadHttpRequestException>(() =>
                QueryRequestBinder.BindAsync(context).AsTask());

            Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
        }

        [Theory]
        [InlineData("price[=")]
        [InlineData("price]=100")]
        [InlineData("[eq]=100")]
        public async Task Should_Throw_400_BadRequest_When_Text_Plain_Payload_Syntax_Is_Malformed(string malformedText) {
            // Arrange
            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = MediaTypeNames.Text.Plain;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(malformedText));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act & Assert
            BadHttpRequestException ex = await Assert.ThrowsAsync<BadHttpRequestException>(() =>
                QueryRequestBinder.BindAsync(context).AsTask());

            Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
        }
    }

    public sealed class HybridSourcePrecedenceAndFallback : QueryRequestBinderTests {
        [Fact]
        public async Task Should_Prioritize_Body_Payload_Over_Url_QueryString_When_Both_Exist() {
            // Arrange
            const string jsonPayload = """{"q": "gaming_laptop", "sort": "-price"}""";

            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = MediaTypeNames.Application.Json;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonPayload));
            context.Request.ContentLength = context.Request.Body.Length;
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["q"] = "office_desk",
                ["sort"] = "createdAt"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("gaming_laptop", result.Q.Value);
            Assert.Equal("-price", result.Sort.ToString());
        }

        [Fact]
        public async Task Should_Fallback_To_Url_QueryString_When_Body_Is_Empty_Stream() {
            // Arrange
            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = MediaTypeNames.Application.Json;
            context.Request.Body = new MemoryStream([]);
            context.Request.ContentLength = 0;
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["q"] = "url_search_term"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("url_search_term", result.Q.Value);
        }

        [Fact]
        public async Task Should_Fallback_To_Url_QueryString_When_ContentType_Is_Missing() {
            // Arrange
            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = null;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("q=ignored"));
            context.Request.ContentLength = 9;
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["q"] = "from_url_parameters"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("from_url_parameters", result.Q.Value);
        }

        [Fact]
        public async Task Should_Return_Empty_QueryRequest_When_Both_Body_And_Url_Are_Empty() {
            // Arrange
            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.True(result.IsEmpty);
        }
    }

    public sealed class OptionsAndCustomParserExtensibility : QueryRequestBinderTests {
        [Fact]
        public async Task Should_Ignore_Body_And_Read_Url_When_AllowBodyPayloads_Option_Is_False() {
            // Arrange
            const string jsonPayload = """{"q": "from_body"}""";

            DefaultHttpContext context = CreateContextWithServices(options => {
                options.AllowBodyPayloads = false;
            });
            context.Request.Method = "QUERY";
            context.Request.ContentType = MediaTypeNames.Application.Json;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonPayload));
            context.Request.ContentLength = context.Request.Body.Length;
            context.Request.Query = new QueryCollection(new Dictionary<string, StringValues> {
                ["q"] = "from_url"
            });

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("from_url", result.Q.Value);
        }

        [Fact]
        public async Task Should_Dispatch_To_Custom_Registered_IQueryPayloadParser() {
            // Arrange
            DefaultHttpContext context = CreateContextWithServices(
                configureServices: services => {
                    services.AddQuerying().AddPayloadParser<YamlQueryPayloadParser>();
                });

            context.Request.Method = "QUERY";
            context.Request.ContentType = "application/x-yaml";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("q: yaml_term"));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("yaml_term", result.Q.Value);
        }

        [Fact]
        public async Task Should_Bind_Successfully_Even_When_RequestServices_Is_Null() {
            // Arrange
            const string jsonPayload = """{"q": "standalone_mode"}""";

            DefaultHttpContext context = new() {
                RequestServices = null!
            };
            context.Request.Method = "QUERY";
            context.Request.ContentType = MediaTypeNames.Application.Json;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonPayload));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("standalone_mode", result.Q.Value);
        }
    }

    public sealed class EncodingAndComplexPayloadIntegrity : QueryRequestBinderTests {
        [Fact]
        public async Task Should_Preserve_Surrogate_Pair_Emojis_And_Unicode_In_Body_Payload() {
            // Arrange
            const string jsonPayload = """
            {
              "q": "Çalışma Masası",
              "filters": [
                { "field": "şehir", "op": "eq", "value": "İstanbul" },
                { "field": "tag", "op": "contains", "value": "🎉😀" }
              ]
            }
            """;

            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = MediaTypeNames.Application.Json;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonPayload));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            Assert.Equal("Çalışma Masası", result.Q.Value);
            Assert.Equal("şehir", result.Filters[0].Field);
            Assert.Equal("İstanbul", result.Filters[0].RawValue);
            Assert.Equal("🎉😀", result.Filters[1].RawValue);
        }

        [Theory]
        [InlineData("formula[eq]=x=y+z", "formula", "x=y+z")]
        [InlineData("token[eq]=dGVzdD1kYXRhPT0=", "token", "dGVzdD1kYXRhPT0=")]
        [InlineData("url[contains]=https://api.example.com/v1/items?id=123", "url", "https://api.example.com/v1/items?id=123")]
        public async Task Should_Preserve_Special_Characters_In_Text_Plain_Body(string line, string expectedField, string expectedValue) {
            // Arrange
            DefaultHttpContext context = CreateContextWithServices();
            context.Request.Method = "QUERY";
            context.Request.ContentType = MediaTypeNames.Text.Plain;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(line));
            context.Request.ContentLength = context.Request.Body.Length;

            // Act
            QueryRequest result = await QueryRequestBinder.BindAsync(context);

            // Assert
            FilterConditionNode filter = Assert.Single(result.Filters);
            Assert.Equal(expectedField, filter.Field);
            Assert.Equal(expectedValue, filter.RawValue);
        }
    }

    #endregion
}