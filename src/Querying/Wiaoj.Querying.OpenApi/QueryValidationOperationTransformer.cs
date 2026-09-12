using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using System.Reflection;
using System.Text.Json.Nodes;
using Wiaoj.Querying.AspNetCore;

namespace Wiaoj.Querying.OpenApi;

/// <summary>
/// Publishes the filter, sort and search surface a schema-validated endpoint actually accepts.
/// </summary>
/// <remarks>
/// <para>
/// <c>QueryRequest</c> binds through <c>BindAsync</c>, which document generation treats as opaque: an
/// endpoint accepting one documents <b>no</b> query parameters at all. So a generated document says the
/// endpoint takes nothing, while in reality it takes a filter language over a fixed set of fields.
/// </para>
/// <para>
/// The schema knows exactly which fields are filterable, which are sortable, and which operators each one
/// permits — it must, in order to reject anything else. This asks it, and writes the same rules into the
/// document, so what is published and what is enforced come from one source.
/// </para>
/// <para>
/// It also follows the application's configuration rather than the schema's defaults: field names as the
/// schema publishes them (through a naming policy when one is configured), types from the field's CLR type,
/// and the same ignored-parameter rules the validation filter applies — so a parameter the validator would
/// skip is not advertised, and one it accepts is not missing.
/// </para>
/// </remarks>
internal sealed class QueryValidationOperationTransformer(QueryOpenApiOptions options) : IOpenApiOperationTransformer {
    private static readonly MethodInfo DescribeFieldsMethod =
        typeof(QuerySchema<>).GetMethod(nameof(QuerySchema<object>.DescribeFields))!;

    public QueryValidationOperationTransformer() : this(new QueryOpenApiOptions()) { }

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) {

        QueryValidationEndpointMetadata? metadata = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<QueryValidationEndpointMetadata>()
            .LastOrDefault();

        if(metadata is null) {
            return Task.CompletedTask;
        }

        object? schema = ResolveSchema(context, metadata.EntityType);
        IReadOnlyList<QueryFieldDescriptor> fields = schema is null ? [] : DescribeFields(schema);

        if(fields.Count == 0) {
            // A schema exposing nothing accepts nothing; inventing parameters would be worse than silence.
            return Task.CompletedTask;
        }

        QueryValidationEndpointOptions? endpointOptions = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<QueryValidationEndpointOptions>()
            .LastOrDefault();

        Func<string, bool> isIgnored = BuildIgnoreRule(context, schema!, endpointOptions);

        operation.Parameters ??= [];

        DescribeFilters(operation, fields, isIgnored);
        DescribeSort(operation, fields, isIgnored);
        DescribeSearch(operation);
        DescribeValidationFailure(operation);

        options.ConfigureOperation?.Invoke(operation, fields);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds one query parameter per filterable field, named the way callers write it.
    /// </summary>
    private void DescribeFilters(
        OpenApiOperation operation,
        IReadOnlyList<QueryFieldDescriptor> fields,
        Func<string, bool> isIgnored) {

        foreach(QueryFieldDescriptor field in fields) {
            if(!field.IsFilterable || isIgnored(field.Name) || HasParameter(operation, field.Name)) {
                continue;
            }

            OpenApiParameter parameter = options.FilterStyle == QueryFilterStyle.DeepObject
                ? DeepObjectFilter(field)
                : ProseFilter(field);

            options.ConfigureFilter?.Invoke(field, parameter);
            operation.Parameters!.Add(parameter);
        }
    }

    private static OpenApiParameter ProseFilter(QueryFieldDescriptor field) {
        string operators = field.AllowedOperators.Count == 0
            ? "no operators configured"
            : string.Join(", ", field.AllowedOperators.Select(QuerySyntax.GetOperatorToken));

        string usage =
            $"Write `{field.Name}=value` for equality, or `{field.Name}[op]=value` where op is one of: {operators}.";

        return new OpenApiParameter {
            Name = field.Name,
            In = ParameterLocation.Query,
            Required = false,
            Description = field.Description is null ? $"Filter on {field.Name}. {usage}" : $"{field.Description} {usage}",
            // The parameter carries the equality shorthand, so it is typed as the field's value.
            Schema = ValueSchema(field.Type)
        };
    }

    /// <summary>
    /// Describes <c>field[op]=value</c> as what OpenAPI calls it: a <c>deepObject</c> whose properties are the
    /// operators the field permits.
    /// </summary>
    private static OpenApiParameter DeepObjectFilter(QueryFieldDescriptor field) {
        Dictionary<string, IOpenApiSchema> operators = new(StringComparer.Ordinal);

        foreach(QueryOperator queryOperator in field.AllowedOperators) {
            operators[QuerySyntax.GetOperatorToken(queryOperator)] = OperatorSchema(queryOperator, field.Type);
        }

        return new OpenApiParameter {
            Name = field.Name,
            In = ParameterLocation.Query,
            Required = false,
            Style = ParameterStyle.DeepObject,
            Explode = true,
            Description = field.Description ?? $"Filter on {field.Name}.",
            Schema = new OpenApiSchema {
                Type = JsonSchemaType.Object,
                Properties = operators,
                AdditionalPropertiesAllowed = false
            }
        };
    }

    private static OpenApiSchema OperatorSchema(QueryOperator queryOperator, Type fieldType) {
        return queryOperator switch {
            QueryOperator.In or QueryOperator.NotIn => new OpenApiSchema {
                Type = JsonSchemaType.String,
                Description = "Comma-separated values. A value cannot itself contain a comma."
            },
            QueryOperator.Between or QueryOperator.NotBetween => new OpenApiSchema {
                Type = JsonSchemaType.String,
                Description = $"Inclusive bounds, written lower{QuerySyntax.RangeDelimiter}upper."
            },
            QueryOperator.IsNull or QueryOperator.IsNotNull => new OpenApiSchema {
                Type = JsonSchemaType.Boolean,
                Description = "Presence is the condition; the value is not read."
            },
            QueryOperator.Contains or QueryOperator.NotContains or
            QueryOperator.StartsWith or QueryOperator.NotStartsWith or
            QueryOperator.EndsWith or QueryOperator.NotEndsWith => new OpenApiSchema { Type = JsonSchemaType.String },
            _ => ValueSchema(fieldType)
        };
    }

    /// <summary>
    /// Types a filter value from the field's CLR type, as the query engine parses it.
    /// </summary>
    private static OpenApiSchema ValueSchema(Type type) {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;

        if(underlying.IsEnum) {
            // The engine parses enum values by name, case-insensitively.
            return new OpenApiSchema {
                Type = JsonSchemaType.String,
                Enum = [.. Enum.GetNames(underlying).Select(name => (JsonNode)JsonValue.Create(name))]
            };
        }

        return underlying switch {
            _ when underlying == typeof(bool) => new OpenApiSchema { Type = JsonSchemaType.Boolean },
            _ when underlying == typeof(byte) || underlying == typeof(sbyte) || underlying == typeof(short) ||
                   underlying == typeof(ushort) || underlying == typeof(int) =>
                new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
            _ when underlying == typeof(uint) || underlying == typeof(long) || underlying == typeof(ulong) =>
                new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" },
            _ when underlying == typeof(float) => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "float" },
            _ when underlying == typeof(double) => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "double" },
            _ when underlying == typeof(decimal) => new OpenApiSchema { Type = JsonSchemaType.Number },
            _ when underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) =>
                new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
            _ when underlying == typeof(DateOnly) => new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" },
            _ when underlying == typeof(TimeOnly) => new OpenApiSchema { Type = JsonSchemaType.String, Format = "time" },
            _ when underlying == typeof(Guid) => new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
            // Strings, strongly-typed identifiers and anything else parsed from text.
            _ => new OpenApiSchema { Type = JsonSchemaType.String }
        };
    }

    private static void DescribeSort(
        OpenApiOperation operation,
        IReadOnlyList<QueryFieldDescriptor> fields,
        Func<string, bool> isIgnored) {

        string[] sortable = [.. fields.Where(f => f.IsSortable && !isIgnored(f.Name)).Select(f => f.Name)];

        if(sortable.Length == 0 || HasParameter(operation, QuerySyntax.Parameters.Sort)) {
            return;
        }

        operation.Parameters!.Add(new OpenApiParameter {
            Name = QuerySyntax.Parameters.Sort,
            In = ParameterLocation.Query,
            Required = false,
            Description = "Comma-separated sort directives; prefix a field with '-' to sort descending. " +
                          $"Sortable fields: {string.Join(", ", sortable)}.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }

    private static void DescribeSearch(OpenApiOperation operation) {
        if(HasParameter(operation, QuerySyntax.Parameters.Q)) {
            return;
        }

        operation.Parameters!.Add(new OpenApiParameter {
            Name = QuerySyntax.Parameters.Q,
            In = ParameterLocation.Query,
            Required = false,
            Description = "Free-text search across the fields the schema marks searchable.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }

    /// <summary>
    /// Records that a query violating the schema is rejected, rather than silently ignored.
    /// </summary>
    private static void DescribeValidationFailure(OpenApiOperation operation) {
        operation.Responses ??= [];

        operation.Responses.TryAdd("400", new OpenApiResponse {
            Description = "The query violates the endpoint's schema — an unknown field, a disallowed operator, "
                        + "or a limit exceeded. The body is a ProblemDetails carrying the validation errors."
        });
    }

    private static bool HasParameter(OpenApiOperation operation, string name) {
        return operation.Parameters?.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) == true;
    }

    /// <summary>
    /// The ignore decision the validation filter makes, for the same inputs: the endpoint's own ignored names,
    /// and the schema's rules over the global ones unless either opts out of global parameters.
    /// </summary>
    private static Func<string, bool> BuildIgnoreRule(
        OpenApiOperationTransformerContext context,
        object schema,
        QueryValidationEndpointOptions? endpointOptions) {

        QueryOptions? global = context.ApplicationServices?.GetService<IOptions<QueryOptions>>()?.Value;
        bool ignoresGlobal = endpointOptions?.IgnoresGlobalParameters == true ||
                             (schema as IQuerySchemaParameters)?.IgnoresGlobalParameters == true;
        QueryOptions? effectiveGlobal = ignoresGlobal ? null : global;

        MethodInfo isIgnored = schema.GetType().GetMethod(
            nameof(QuerySchema<object>.IsParameterIgnored),
            [typeof(string), typeof(QueryOptions)])!;

        return name =>
            endpointOptions?.IgnoredParameters.Contains(name) == true ||
            (bool)isIgnored.Invoke(schema, [name, effectiveGlobal])!;
    }

    private static object? ResolveSchema(OpenApiOperationTransformerContext context, Type entityType) {
        return context.ApplicationServices?.GetService(typeof(QuerySchema<>).MakeGenericType(entityType));
    }

    /// <summary>
    /// Asks the schema to describe itself. The entity type is only known at run time, so the call is made
    /// reflectively — over a method the schema exposes publicly, not around its encapsulation.
    /// </summary>
    private static IReadOnlyList<QueryFieldDescriptor> DescribeFields(object schema) {
        MethodInfo describe = schema.GetType().GetMethod(DescribeFieldsMethod.Name)!;
        return (IReadOnlyList<QueryFieldDescriptor>)describe.Invoke(schema, null)!;
    }
}
