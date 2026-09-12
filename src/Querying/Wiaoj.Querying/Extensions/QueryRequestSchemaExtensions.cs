namespace Wiaoj.Querying;

/// <summary>
/// Operations on a <see cref="QueryRequest"/> that need to know which schema it is meant for.
/// </summary>
public static class QueryRequestSchemaExtensions {

    /// <summary>
    /// Splits a request into the part <paramref name="schema"/> owns and the part it does not.
    /// </summary>
    /// <typeparam name="T">The entity the schema describes.</typeparam>
    /// <param name="request">The incoming request.</param>
    /// <param name="schema">The schema that owns one part of it.</param>
    /// <returns>
    /// <c>Owned</c>: filters and sorts on fields the schema declares, plus the search term when the schema
    /// searches. <c>Remainder</c>: everything else, untouched, for the caller to validate elsewhere or forward.
    /// </returns>
    /// <remarks>
    /// <para>
    /// An endpoint whose filters belong to more than one owner — some are columns of this entity, some are
    /// answered by another service — otherwise has to stop the local validator rejecting the foreign ones by
    /// listing them in <c>IgnoreParameters</c>, which means they are neither validated nor described.
    /// </para>
    /// <para>
    /// The split is by <b>ownership</b>, not by permission. A filter on a field the schema declares goes to
    /// <c>Owned</c> even when its operator is not allowed there, so the local validation rejects it loudly.
    /// Sending a locally-owned field on to another service because this one refused the operator would turn a
    /// 400 into a different query answered somewhere else.
    /// </para>
    /// <para>
    /// Parameters the schema explicitly ignores are not owned, and go to the remainder.
    /// </para>
    /// </remarks>
    public static (QueryRequest Owned, QueryRequest Remainder) Partition<T>(
        this QueryRequest request,
        QuerySchema<T> schema) {

        ArgumentNullException.ThrowIfNull(schema);

        List<FilterConditionNode> ownedFilters = [];
        List<FilterConditionNode> otherFilters = [];

        foreach(FilterConditionNode filter in request.Filters) {
            (Owns(schema, filter.Field) ? ownedFilters : otherFilters).Add(filter);
        }

        List<SortNode> ownedSort = [];
        List<SortNode> otherSort = [];

        foreach(SortNode node in request.Sort.Nodes) {
            (Owns(schema, node.Field) ? ownedSort : otherSort).Add(node);
        }

        bool searches = schema.SearchSelectors.Count > 0;

        QueryRequest owned = new(
            searches ? request.Q : Q.Empty,
            ToSort(ownedSort),
            ownedFilters);

        QueryRequest remainder = new(
            searches ? Q.Empty : request.Q,
            ToSort(otherSort),
            otherFilters);

        return (owned, remainder);
    }

    private static bool Owns<T>(QuerySchema<T> schema, string field) {
        return !schema.IsParameterIgnored(field) && schema.TryGetProperty(field, out _);
    }

    private static Sort ToSort(List<SortNode> nodes) {
        return nodes.Count == 0 ? Sort.Empty : new Sort([.. nodes]);
    }
}
