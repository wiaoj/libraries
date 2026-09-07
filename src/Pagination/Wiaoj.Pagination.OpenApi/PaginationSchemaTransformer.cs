using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wiaoj.Pagination.OpenApi;

/// <summary>
/// Describes the pagination envelopes in the generated document.
/// </summary>
/// <remarks>
/// <para>
/// Schema generation reads properties off the JSON contract, and these types have none it can see: their wire
/// shape is produced by hand-written converters, and the properties that do exist are an
/// <c>EquatableArray&lt;T&gt;</c> and a <c>CursorToken</c> rather than an array and a string. The exporter's
/// answer to a type it cannot read is an empty schema, which a client generator renders as <c>unknown</c> —
/// so a consumer switching from offset to keyset paging loses the typed result they had.
/// </para>
/// <para>
/// The shapes below are written from the converters, which are the actual wire contract, and are pinned by
/// tests that serialise a value and check the document against what came out.
/// </para>
/// </remarks>
internal sealed class PaginationSchemaTransformer : IOpenApiSchemaTransformer {
    private const string Items = "items";
    private const string Metadata = "metadata";

    public async Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken) {

        Type type = context.JsonTypeInfo.Type;

        if(type == typeof(CursorToken)) {
            DescribeCursorToken(schema);
            return;
        }

        if(type == typeof(CursorMetadata)) {
            DescribeCursorMetadata(schema);
            return;
        }

        if(type == typeof(PageMetadata)) {
            DescribePageMetadata(schema);
            return;
        }

        if(!type.IsGenericType) {
            return;
        }

        Type definition = type.GetGenericTypeDefinition();

        if(definition == typeof(CursorResult<>)) {
            await DescribeEnvelopeAsync(schema, context, type, typeof(CursorMetadata), cancellationToken)
                .ConfigureAwait(false);
        }
        else if(definition == typeof(PagedResult<>)) {
            await DescribeEnvelopeAsync(schema, context, type, typeof(PageMetadata), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Describes the envelope, resolving the item and metadata schemas through the document so they are
    /// shared rather than duplicated inline at every endpoint that pages.
    /// </summary>
    private static async Task DescribeEnvelopeAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        Type resultType,
        Type metadataType,
        CancellationToken cancellationToken) {

        Type itemType = resultType.GetGenericArguments()[0];

        IOpenApiSchema itemSchema = await context
            .GetOrCreateSchemaAsync(itemType, parameterDescription: null, cancellationToken)
            .ConfigureAwait(false);

        IOpenApiSchema metadataSchema = await context
            .GetOrCreateSchemaAsync(metadataType, parameterDescription: null, cancellationToken)
            .ConfigureAwait(false);

        schema.Type = JsonSchemaType.Object;
        schema.Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) {
            [Items] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = itemSchema },
            [Metadata] = metadataSchema
        };
        schema.Required = new HashSet<string>(StringComparer.Ordinal) { Items, Metadata };
    }

    /// <summary>
    /// A cursor is an opaque Base64Url string. Its struct shape is deliberately not described: a caller sends
    /// back what it was given, and has no business reading inside it.
    /// </summary>
    private static void DescribeCursorToken(OpenApiSchema schema) {
        schema.Type = JsonSchemaType.String | JsonSchemaType.Null;
        schema.Properties = null;
        schema.Description = "Opaque Base64Url cursor. Send it back verbatim; it has no client-readable structure.";
    }

    private static void DescribeCursorMetadata(OpenApiSchema schema) {
        schema.Type = JsonSchemaType.Object;
        schema.Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) {
            ["startCursor"] = Cursor("Cursor of the first item in this window, or null when the window is empty."),
            ["endCursor"] = Cursor("Cursor of the last item in this window, or null when the window is empty."),
            ["hasPrevious"] = Flag("Whether items exist before this window."),
            ["hasNext"] = Flag("Whether items exist after this window.")
        };
        schema.Required = new HashSet<string>(StringComparer.Ordinal) {
            "startCursor", "endCursor", "hasPrevious", "hasNext"
        };

        static OpenApiSchema Cursor(string description) {
            return new OpenApiSchema {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Description = description
            };
        }
    }

    private static void DescribePageMetadata(OpenApiSchema schema) {
        schema.Type = JsonSchemaType.Object;
        schema.Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) {
            ["totalCount"] = Count("int64", "Total number of items across all pages."),
            ["page"] = Count("int32", "1-based index of this page."),
            ["size"] = Count("int32", "Requested page size."),
            ["totalPages"] = Count("int64", "Number of pages at this page size."),
            ["hasPrevious"] = Flag("Whether a page exists before this one."),
            ["hasNext"] = Flag("Whether a page exists after this one.")
        };
        schema.Required = new HashSet<string>(StringComparer.Ordinal) {
            "totalCount", "page", "size", "totalPages", "hasPrevious", "hasNext"
        };

        static OpenApiSchema Count(string format, string description) {
            return new OpenApiSchema {
                Type = JsonSchemaType.Integer,
                Format = format,
                Description = description
            };
        }
    }

    private static OpenApiSchema Flag(string description) {
        return new OpenApiSchema { Type = JsonSchemaType.Boolean, Description = description };
    }
}
