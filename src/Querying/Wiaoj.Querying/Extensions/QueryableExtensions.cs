
using Wiaoj.Querying.Compilers;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying;
/// <summary>
/// Provides extension methods for applying query filtering and searching to <see cref="IQueryable{T}"/>.
/// </summary>
public static class QueryableExtensions {
    private static readonly BracketQueryParser Parser = new();

    /// <summary>
    /// Applies dynamic query filters and search terms to the target queryable using pure key-value parameters.
    /// </summary>
    public static IQueryable<T> ApplyQuery<T>(
        this IQueryable<T> source,
        IEnumerable<KeyValuePair<string, string?>> parameters,
        Action<QuerySchema<T>> configureSchema) {
        QuerySchema<T> schema = new();
        configureSchema(schema);

        List<FilterConditionNode> conditions = new();
        string? searchTerm = null;

        foreach(var (key, value) in parameters) {
            if(string.Equals(key, "q", StringComparison.OrdinalIgnoreCase)) {
                searchTerm = value;
                continue;
            }

            var rawParam = value is null ? key : $"{key}={value}";
            if(Parser.TryParse(rawParam, out var condition)) {
                conditions.Add(condition);
            }
        }

        var searchPredicate = ExpressionCompiler.CompileSearch(searchTerm, schema);
        if(searchPredicate != null) {
            source = source.Where(searchPredicate);
        }

        var filterPredicate = ExpressionCompiler.CompileFilters(conditions, schema);
        if(filterPredicate != null) {
            source = source.Where(filterPredicate);
        }

        return source;
    }
}