using Wiaoj.Preconditions;

namespace Wiaoj.Querying;

public readonly partial record struct QueryRequest {

    /// <summary>
    /// Starts a request built from values. See <see cref="QueryRequestBuilder"/>.
    /// </summary>
    public static QueryRequestBuilder CreateBuilder() => new();

    /// <summary>
    /// Keeps only the filters and sort directives on the named fields.
    /// </summary>
    /// <param name="fields">The exposed field names to keep, compared case-insensitively.</param>
    /// <returns>A request carrying only those fields.</returns>
    /// <remarks>
    /// The free-text search term is dropped. It belongs to no single field, so "the part of this query about
    /// <c>locale</c>" does not include it. Use <see cref="Without"/> to remove fields and keep the search.
    /// </remarks>
    public QueryRequest Only(params ReadOnlySpan<string> fields) {
        HashSet<string> keep = ToSet(fields);

        return new QueryRequest(
            Q.Empty,
            FilterSort(this.Sort, node => keep.Contains(node.Field)),
            [.. this.Filters.Where(filter => keep.Contains(filter.Field))]);
    }

    /// <summary>
    /// Removes the filters and sort directives on the named fields.
    /// </summary>
    /// <param name="fields">The exposed field names to remove, compared case-insensitively.</param>
    /// <returns>A request without those fields. The search term is kept.</returns>
    public QueryRequest Without(params ReadOnlySpan<string> fields) {
        HashSet<string> drop = ToSet(fields);

        return new QueryRequest(
            this.Q,
            FilterSort(this.Sort, node => !drop.Contains(node.Field)),
            [.. this.Filters.Where(filter => !drop.Contains(filter.Field))]);
    }

    /// <summary>
    /// Combines two requests into one that satisfies both.
    /// </summary>
    /// <param name="first">The request whose sort takes precedence.</param>
    /// <param name="second">The request merged into it.</param>
    /// <returns>The combined request.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Filters</b> are all kept, and all must hold — filters combine with AND when
    /// applied. Two filters on one field are not reconciled: <c>status=a</c> merged with <c>status=b</c>
    /// matches nothing, which is what both together mean.</description></item>
    /// <item><description><b>Sort</b> is <paramref name="first"/>'s directives followed by
    /// <paramref name="second"/>'s, skipping any field <paramref name="first"/> already sorts on — so the
    /// second request can refine the order but never override it.</description></item>
    /// <item><description><b>Search</b> is taken from whichever request has one. Two different terms cannot
    /// both apply, and choosing one silently would drop the other's intent, so that throws.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentException">Both requests carry a search term, and they differ.</exception>
    public static QueryRequest Merge(QueryRequest first, QueryRequest second) {
        Q search = (first.Q.IsEmpty, second.Q.IsEmpty) switch {
            (true, _) => second.Q,
            (_, true) => first.Q,
            _ when string.Equals(first.Q.Value, second.Q.Value, StringComparison.Ordinal) => first.Q,
            _ => throw new ArgumentException(
                $"Both requests carry a search term ('{first.Q.Value}' and '{second.Q.Value}'). Only one can apply.",
                nameof(second))
        };

        List<SortNode> sort = [.. first.Sort.Nodes];
        HashSet<string> sorted = new(sort.Select(node => node.Field), StringComparer.OrdinalIgnoreCase);

        foreach(SortNode node in second.Sort.Nodes) {
            if(sorted.Add(node.Field)) {
                sort.Add(node);
            }
        }

        return new QueryRequest(
            search,
            sort.Count == 0 ? Sort.Empty : new Sort([.. sort]),
            [.. first.Filters, .. second.Filters]);
    }

    private static HashSet<string> ToSet(ReadOnlySpan<string> fields) {
        HashSet<string> set = new(StringComparer.OrdinalIgnoreCase);

        foreach(string field in fields) {
            Preca.ThrowIfNullOrWhiteSpace(field);
            set.Add(field.Trim());
        }

        return set;
    }

    private static Sort FilterSort(Sort sort, Func<SortNode, bool> predicate) {
        SortNode[] kept = [.. sort.Nodes.Where(predicate)];
        return kept.Length == 0 ? Sort.Empty : new Sort(kept);
    }
}
