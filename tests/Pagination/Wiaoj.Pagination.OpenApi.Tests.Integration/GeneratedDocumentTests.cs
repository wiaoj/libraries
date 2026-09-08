using System.Text.Json;
using Wiaoj.Pagination.OpenApi.Tests.Integration.Fixtures;
using Wiaoj.Primitives.Collections;

namespace Wiaoj.Pagination.OpenApi.Tests.Integration;

/// <summary>
/// Reads the document the generator actually produces for endpoints that page.
/// </summary>
/// <remarks>
/// A result type whose schema comes out empty is invisible to every test that checks a transformer's output,
/// and invisible in the running application too — the responses are correct, only the document is silent.
/// It surfaces one step further out, in a generated client, as a response typed <c>unknown</c>. These read
/// the document itself, which is the first place the gap is visible.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Feature", "Pagination")]
[Trait("Component", "OpenApi")]
public static class GeneratedDocumentTests {

    private static IReadOnlyList<string> PropertyNames(JsonElement schema) {
        return [.. schema.GetProperty("properties").EnumerateObject().Select(p => p.Name)];
    }

    public sealed class TheKeysetEnvelope(DocumentFixture fixture) : IClassFixture<DocumentFixture> {
        [Fact]
        public void Should_Be_Described_Rather_Than_Left_Empty() {
            JsonElement schema = fixture.Schema("CursorResultOfProduct");

            Assert.Equal("object", schema.GetProperty("type").GetString());
            Assert.Equal(["items", "metadata"], PropertyNames(schema));
        }

        [Fact]
        public void Should_Type_Its_Items_As_An_Array_Of_The_Item_Type() {
            JsonElement items = fixture.Schema("CursorResultOfProduct").GetProperty("properties").GetProperty("items");

            Assert.Equal("array", items.GetProperty("type").GetString());
            Assert.Equal("#/components/schemas/Product", items.GetProperty("items").GetProperty("$ref").GetString());
        }

        [Fact]
        public void Should_Reference_The_Shared_Metadata_Schema_Rather_Than_Inline_It() {
            JsonElement metadata = fixture.Schema("CursorResultOfProduct").GetProperty("properties").GetProperty("metadata");

            Assert.Equal("#/components/schemas/CursorMetadata", metadata.GetProperty("$ref").GetString());
        }

        [Fact]
        public void Should_Describe_Cursors_As_Nullable_Strings_Rather_Than_Their_Struct_Shape() {
            JsonElement start = fixture.Schema("CursorMetadata").GetProperty("properties").GetProperty("startCursor");
            string[] types = [.. start.GetProperty("type").EnumerateArray().Select(t => t.GetString()!)];

            Assert.Contains("string", types);
            Assert.Contains("null", types);
        }
    }

    public sealed class TheOffsetEnvelope(DocumentFixture fixture) : IClassFixture<DocumentFixture> {
        [Fact]
        public void Should_Be_Described_Rather_Than_Left_Empty() {
            JsonElement schema = fixture.Schema("PagedResultOfProduct");

            Assert.Equal("object", schema.GetProperty("type").GetString());
            Assert.Equal(["items", "metadata"], PropertyNames(schema));
        }

        [Fact]
        public void Should_Reference_The_Shared_Metadata_Schema() {
            JsonElement metadata = fixture.Schema("PagedResultOfProduct").GetProperty("properties").GetProperty("metadata");

            Assert.Equal("#/components/schemas/PageMetadata", metadata.GetProperty("$ref").GetString());
        }

        [Fact]
        public void Should_Widen_The_Counts_That_Are_64_Bit() {
            JsonElement properties = fixture.Schema("PageMetadata").GetProperty("properties");

            Assert.Equal("int64", properties.GetProperty("totalCount").GetProperty("format").GetString());
            Assert.Equal("int64", properties.GetProperty("totalPages").GetProperty("format").GetString());
            Assert.Equal("int32", properties.GetProperty("page").GetProperty("format").GetString());
        }
    }

    /// <summary>
    /// The schemas are written by hand from the converters, so they can drift from them silently. These
    /// serialise a value and hold the document to what came out.
    /// </summary>
    public sealed class TheSchemaAgainstTheWire(DocumentFixture fixture) : IClassFixture<DocumentFixture> {
        private static IReadOnlyList<string> Serialised<T>(T value) {
            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(value));
            return [.. document.RootElement.EnumerateObject().Select(p => p.Name)];
        }

        [Fact]
        public void Should_Match_The_Keyset_Envelope() {
            CursorResult<Product> result = new(new EquatableArray<Product>([]), CursorMetadata.Empty);

            Assert.Equal(Serialised(result), PropertyNames(fixture.Schema("CursorResultOfProduct")));
        }

        [Fact]
        public void Should_Match_The_Keyset_Metadata() {
            Assert.Equal(Serialised(CursorMetadata.Empty), PropertyNames(fixture.Schema("CursorMetadata")));
        }

        [Fact]
        public void Should_Match_The_Offset_Envelope() {
            PagedResult<Product> result = new(new EquatableArray<Product>([]), PageMetadata.Empty);

            Assert.Equal(Serialised(result), PropertyNames(fixture.Schema("PagedResultOfProduct")));
        }

        [Fact]
        public void Should_Match_The_Offset_Metadata() {
            Assert.Equal(Serialised(new PageMetadata(1, 1, 20)), PropertyNames(fixture.Schema("PageMetadata")));
        }
    }

    /// <summary>
    /// A handler taking a request record directly receives it from one query value in a compact format.
    /// </summary>
    public sealed class TheCompactRequest(DocumentFixture fixture) : IClassFixture<DocumentFixture> {
        [Fact]
        public void Should_Explain_The_Grammar_Of_The_Single_Value_It_Binds() {
            JsonElement parameter = fixture.Parameters("/compact").EnumerateArray().Single();

            Assert.Equal("paging", parameter.GetProperty("name").GetString());
            Assert.Contains("cursor:limit:direction", parameter.GetProperty("description").GetString());
        }

        [Fact]
        public void Should_Not_Also_Document_Parameters_The_Endpoint_Does_Not_Accept() {
            IReadOnlyList<string> names = [.. fixture.Parameters("/compact")
                .EnumerateArray()
                .Select(p => p.GetProperty("name").GetString()!)];

            Assert.DoesNotContain(PaginationParameters.Cursor, names);
            Assert.DoesNotContain(PaginationParameters.Limit, names);
        }

        [Fact]
        public void Should_Describe_A_Union_Returning_Endpoint_As_Paging() {
            IReadOnlyList<string> names = [.. fixture.Parameters("/union")
                .EnumerateArray()
                .Select(p => p.GetProperty("name").GetString()!)];

            Assert.Contains(PaginationParameters.Cursor, names);
            Assert.Contains(PaginationParameters.Limit, names);
        }

        [Fact]
        public void Should_Leave_The_Expanded_Form_Alone() {
            IReadOnlyList<string> names = [.. fixture.Parameters("/keyset")
                .EnumerateArray()
                .Select(p => p.GetProperty("name").GetString()!)];

            Assert.Contains(PaginationParameters.Cursor, names);
            Assert.Contains(PaginationParameters.Limit, names);
            Assert.Contains(PaginationParameters.Direction, names);
        }
    }
}
