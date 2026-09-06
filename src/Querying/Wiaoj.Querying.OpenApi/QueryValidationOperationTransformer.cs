using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using System.Reflection;
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
/// </remarks>
internal sealed class QueryValidationOperationTransformer : IOpenApiOperationTransformer {
    private static readonly MethodInfo DescribeFieldsMethod =
        typeof(QuerySchema<>).GetMethod(nameof(QuerySchema<object>.DescribeFields))!;

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

        IReadOnlyList<QueryFieldDescriptor> fields = DescribeFields(context, metadata.EntityType);

        if(fields.Count == 0) {
            // A schema exposing nothing accepts nothing; inventing parameters would be worse than silence.
            return Task.CompletedTask;
        }

        QueryValidationEndpointOptions? endpointOptions = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<QueryValidationEndpointOptions>()
            .LastOrDefault();

        operation.Parameters ??= [];

        DescribeFilters(operation, fields, endpointOptions);
        DescribeSort(operation, fields);
        DescribeSearch(operation);
        DescribeValidationFailure(operation);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds one query parameter per filterable field, named the way callers write it, with the operators that
    /// field permits spelled out.
    /// </summary>
    private static void DescribeFilters(
        OpenApiOperation operation,
        IReadOnlyList<QueryFieldDescriptor> fields,
        QueryValidationEndpointOptions? endpointOptions) {

        foreach(QueryFieldDescriptor field in fields) {
            if(!field.IsFilterable || endpointOptions?.IgnoredParameters.Contains(field.Name) == true) {
                continue;
            }

            string operators = field.AllowedOperators.Count == 0
                ? "no operators configured"
                : string.Join(", ", field.AllowedOperators.Select(GetOperatorToken));

            AddIfMissing(operation, field.Name, JsonSchemaType.String,
                $"Filter on {field.Name}. Write `{field.Name}=value` for equality, or " +
                $"`{field.Name}[op]=value` where op is one of: {operators}.");
        }
    }

    private static void DescribeSort(OpenApiOperation operation, IReadOnlyList<QueryFieldDescriptor> fields) {
        string[] sortable = [.. fields.Where(f => f.IsSortable).Select(f => f.Name)];

        if(sortable.Length == 0) {
            return;
        }

        AddIfMissing(operation, QuerySyntax.Parameters.Sort, JsonSchemaType.String,
            $"Comma-separated sort directives; prefix a field with '-' to sort descending. " +
            $"Sortable fields: {string.Join(", ", sortable)}.");
    }

    private static void DescribeSearch(OpenApiOperation operation) {
        AddIfMissing(operation, QuerySyntax.Parameters.Q, JsonSchemaType.String,
            "Free-text search across the fields the schema marks searchable.");
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

    private static void AddIfMissing(OpenApiOperation operation, string name, JsonSchemaType type, string description) {
        operation.Parameters ??= [];

        if(operation.Parameters.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter {
            Name = name,
            In = ParameterLocation.Query,
            Required = false,
            Description = description,
            Schema = new OpenApiSchema { Type = type }
        });
    }

    /// <summary>
    /// Resolves <c>QuerySchema&lt;TEntity&gt;</c> from the container and asks it to describe itself.
    /// </summary>
    /// <remarks>
    /// The entity type is only known at run time, so the call is made reflectively — over a method the schema
    /// exposes publicly, not around its encapsulation.
    /// </remarks>
    private static IReadOnlyList<QueryFieldDescriptor> DescribeFields(
        OpenApiOperationTransformerContext context,
        Type entityType) {

        Type schemaType = typeof(QuerySchema<>).MakeGenericType(entityType);
        object? schema = context.ApplicationServices.GetService(schemaType);

        if(schema is null) {
            return [];
        }

        MethodInfo describe = schemaType.GetMethod(DescribeFieldsMethod.Name)!;
        return (IReadOnlyList<QueryFieldDescriptor>)describe.Invoke(schema, null)!;
    }

    private static string GetOperatorToken(QueryOperator queryOperator) {
        return queryOperator switch {
            QueryOperator.Equal => QuerySyntax.Operators.Equal,
            QueryOperator.NotEqual => QuerySyntax.Operators.NotEqual,
            QueryOperator.GreaterThan => QuerySyntax.Operators.GreaterThan,
            QueryOperator.GreaterThanOrEqual => QuerySyntax.Operators.GreaterThanOrEqual,
            QueryOperator.LessThan => QuerySyntax.Operators.LessThan,
            QueryOperator.LessThanOrEqual => QuerySyntax.Operators.LessThanOrEqual,
            QueryOperator.Contains => QuerySyntax.Operators.Contains,
            QueryOperator.NotContains => QuerySyntax.Operators.NotContains,
            QueryOperator.StartsWith => QuerySyntax.Operators.StartsWith,
            QueryOperator.NotStartsWith => QuerySyntax.Operators.NotStartsWith,
            QueryOperator.EndsWith => QuerySyntax.Operators.EndsWith,
            QueryOperator.NotEndsWith => QuerySyntax.Operators.NotEndsWith,
            QueryOperator.In => QuerySyntax.Operators.In,
            QueryOperator.NotIn => QuerySyntax.Operators.NotIn,
            QueryOperator.Between => QuerySyntax.Operators.Between,
            _ => queryOperator.ToString()
        };
    }
}
