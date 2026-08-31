using Microsoft.AspNetCore.Http;
using Wiaoj.Preconditions;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.AspNetCore.Binders;

/// <summary>
/// Internal parameter binder resolving <see cref="QueryRequest"/> instances from HTTP request contexts.
/// </summary>
internal static class QueryRequestBinder {
    /// <summary>
    /// Asynchronously binds a <see cref="QueryRequest"/> from the incoming HTTP request query collection.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A value task containing the parsed <see cref="QueryRequest"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static ValueTask<QueryRequest> BindAsync(HttpContext context) {
        Preca.ThrowIfNull(context);

        IQueryCollection query = context.Request.Query;
        if(query.Count == 0) {
            return ValueTask.FromResult(QueryRequest.Empty);
        }

        Q q = default;
        Sort sort = default;
        List<FilterConditionNode>? filters = null;

        foreach((string? key, Microsoft.Extensions.Primitives.StringValues stringValues) in query) {
            if(string.IsNullOrWhiteSpace(key)) {
                continue;
            }

            string trimmedKey = key.Trim();

            if(trimmedKey.Equals(QuerySyntax.Parameters.Q, StringComparison.OrdinalIgnoreCase)) {
                q = new Q(stringValues.ToString());
                continue;
            }

            if(trimmedKey.Equals(QuerySyntax.Parameters.Sort, StringComparison.OrdinalIgnoreCase)) {
                if(Sort.TryParse(stringValues.ToString(), out Sort parsedSort)) {
                    sort = parsedSort;
                }
                continue;
            }

            // Case A: QueryCollection parameter with no values (e.g. "?deletedAt[isNull]" -> StringValues.Empty)
            if(stringValues.Count == 0) {
                if(BracketQueryParser.TryParse(trimmedKey, out FilterConditionNode unaryNode)) {
                    filters ??= [];
                    filters.Add(unaryNode);
                }
                continue;
            }

            // Case B: QueryCollection parameter with one or more values
            for(int i = 0; i < stringValues.Count; i++) {
                string? val = stringValues[i];

                if(string.IsNullOrEmpty(val) && BracketQueryParser.TryParse(trimmedKey, out FilterConditionNode unaryNode)) {
                    filters ??= [];
                    filters.Add(unaryNode);
                    continue;
                }

                string rawPair = $"{trimmedKey}={val}";
                if(BracketQueryParser.TryParse(rawPair, out FilterConditionNode filterNode)) {
                    filters ??= [];
                    filters.Add(filterNode);
                }
            }
        }

        return ValueTask.FromResult(new QueryRequest(q: q, sort: sort, filters: filters));
    }
}