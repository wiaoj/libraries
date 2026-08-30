namespace Wiaoj.Querying.Tests.Integration.Builders;

using System.Text;
using Wiaoj.Querying;

/// <summary>
/// Fluent builder for constructing query strings and URLs for testing and client requests.
/// </summary>
public sealed class QueryBuilder {
    private readonly StringBuilder _query = new();
    private bool _hasParameters;

    /// <summary>
    /// Creates a new instance of <see cref="QueryBuilder"/>.
    /// </summary>
    public static QueryBuilder Create() => new();

    /// <summary>
    /// Adds a free-text search parameter (<c>q=term</c>).
    /// </summary>
    public QueryBuilder Search(string searchTerm) {
        AppendSeparator();
        _query.Append("q=").Append(Uri.EscapeDataString(searchTerm));
        return this;
    }

    /// <summary>
    /// Adds a bracket-style filter condition (<c>field[op]=value</c>).
    /// </summary>
    public QueryBuilder Where(string field, QueryOperator op, object value) {
        AppendSeparator();
        var opString = MapOperatorToString(op);
        _query.Append(field)
              .Append('[')
              .Append(opString)
              .Append("]=")
              .Append(Uri.EscapeDataString(value.ToString() ?? string.Empty));
        return this;
    }

    /// <summary>
    /// Adds a sort parameter (e.g. <c>sort=-price,createdAt</c>).
    /// </summary>
    public QueryBuilder Sort(string sortExpression) {
        AppendSeparator();
        _query.Append("sort=").Append(Uri.EscapeDataString(sortExpression));
        return this;
    }

    /// <summary>
    /// Adds a limit parameter.
    /// </summary>
    public QueryBuilder Limit(int limit) {
        AppendSeparator();
        _query.Append("limit=").Append(limit);
        return this;
    }

    /// <summary>
    /// Builds the complete URL by combining base path and query parameters.
    /// </summary>
    public string BuildUrl(string basePath) {
        if(_query.Length == 0) {
            return basePath;
        }

        return $"{basePath}?{_query}";
    }

    private void AppendSeparator() {
        if(_hasParameters) {
            _query.Append('&');
        }
        else {
            _hasParameters = true;
        }
    }

    private static string MapOperatorToString(QueryOperator op) => op switch {
        QueryOperator.Equal => "eq",
        QueryOperator.NotEqual => "neq",
        QueryOperator.GreaterThan => "gt",
        QueryOperator.GreaterThanOrEqual => "gte",
        QueryOperator.LessThan => "lt",
        QueryOperator.LessThanOrEqual => "lte",
        QueryOperator.Contains => "contains",
        QueryOperator.StartsWith => "startsWith",
        QueryOperator.EndsWith => "endsWith",
        QueryOperator.In => "in",
        QueryOperator.NotIn => "notIn",
        QueryOperator.Between => "between",
        QueryOperator.IsNull => "isNull",
        QueryOperator.IsNotNull => "isNotNull",
        _ => "eq"
    };
}