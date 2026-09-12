using System.Globalization;
using Wiaoj.Preconditions;

namespace Wiaoj.Querying;

/// <summary>
/// Builds a <see cref="QueryRequest"/> from values rather than from hand-assembled strings.
/// </summary>
/// <remarks>
/// <para>
/// A query built by hand has to reproduce the wire syntax exactly — join an <c>in</c> list with commas, put
/// <c>..</c> between range bounds, format a date the way the other side will parse it. Every one of those is
/// a place for a request to be silently different from the one intended. This owns the rendering.
/// </para>
/// <para>
/// It also refuses what the language cannot express, instead of producing something that parses as a
/// different query:
/// </para>
/// <list type="bullet">
/// <item><description>A value containing <c>,</c> in an <c>in</c> list. The list is split on commas with no
/// escaping, so <c>"a,b"</c> would arrive as two values.</description></item>
/// <item><description>An empty <c>in</c> list. It is ignored when applied, so it matches every row — the
/// opposite of what an empty set of allowed values means.</description></item>
/// <item><description>A range bound containing <c>..</c>, which is the range delimiter.</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// QueryRequest query = QueryRequest.CreateBuilder()
///     .In("keyId", pageKeyIds.Select(id => id.Encode()))
///     .In("locale", requestedLocales)
///     .OrderBy("locale")
///     .Build();
/// </code>
/// </example>
public sealed class QueryRequestBuilder {
    private readonly List<FilterConditionNode> _filters = [];
    private readonly List<SortNode> _sort = [];
    private Q _search;

    internal QueryRequestBuilder() { }

    /// <summary>Adds a filter with an explicit operator and an already-rendered raw value.</summary>
    /// <remarks>An escape hatch for operators without a dedicated method; the value is not checked.</remarks>
    public QueryRequestBuilder Where(string field, QueryOperator op, string? rawValue) {
        this._filters.Add(new FilterConditionNode(RequireField(field), op, rawValue));
        return this;
    }

    /// <summary>Adds an existing filter node.</summary>
    public QueryRequestBuilder Where(FilterConditionNode filter) {
        if(filter.IsEmpty) {
            throw new ArgumentException("A filter must name a field.", nameof(filter));
        }

        this._filters.Add(filter);
        return this;
    }

    /// <summary>Adds <c>field = value</c>.</summary>
    public QueryRequestBuilder Equal(string field, object? value) => Add(field, QueryOperator.Equal, value);

    /// <summary>Adds <c>field != value</c>.</summary>
    public QueryRequestBuilder NotEqual(string field, object? value) => Add(field, QueryOperator.NotEqual, value);

    /// <summary>Adds <c>field &gt; value</c>.</summary>
    public QueryRequestBuilder GreaterThan(string field, object value) => Add(field, QueryOperator.GreaterThan, RequireValue(value));

    /// <summary>Adds <c>field &gt;= value</c>.</summary>
    public QueryRequestBuilder GreaterThanOrEqual(string field, object value) => Add(field, QueryOperator.GreaterThanOrEqual, RequireValue(value));

    /// <summary>Adds <c>field &lt; value</c>.</summary>
    public QueryRequestBuilder LessThan(string field, object value) => Add(field, QueryOperator.LessThan, RequireValue(value));

    /// <summary>Adds <c>field &lt;= value</c>.</summary>
    public QueryRequestBuilder LessThanOrEqual(string field, object value) => Add(field, QueryOperator.LessThanOrEqual, RequireValue(value));

    /// <summary>Adds a substring match.</summary>
    public QueryRequestBuilder Contains(string field, string value) => Add(field, QueryOperator.Contains, RequireValue(value));

    /// <summary>Adds a prefix match.</summary>
    public QueryRequestBuilder StartsWith(string field, string value) => Add(field, QueryOperator.StartsWith, RequireValue(value));

    /// <summary>Adds a suffix match.</summary>
    public QueryRequestBuilder EndsWith(string field, string value) => Add(field, QueryOperator.EndsWith, RequireValue(value));

    /// <summary>Adds <c>field IN (values)</c>.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="values"/> is empty, or one of them contains a comma.
    /// </exception>
    public QueryRequestBuilder In<TValue>(string field, IEnumerable<TValue> values) =>
        AddList(field, QueryOperator.In, values);

    /// <summary>Adds <c>field NOT IN (values)</c>.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="values"/> is empty, or one of them contains a comma.
    /// </exception>
    public QueryRequestBuilder NotIn<TValue>(string field, IEnumerable<TValue> values) =>
        AddList(field, QueryOperator.NotIn, values);

    /// <summary>Adds an inclusive range.</summary>
    /// <exception cref="ArgumentException">A bound contains the range delimiter <c>..</c>.</exception>
    public QueryRequestBuilder Between(string field, object lower, object upper) =>
        AddRange(field, QueryOperator.Between, lower, upper);

    /// <summary>Adds an excluded range.</summary>
    /// <exception cref="ArgumentException">A bound contains the range delimiter <c>..</c>.</exception>
    public QueryRequestBuilder NotBetween(string field, object lower, object upper) =>
        AddRange(field, QueryOperator.NotBetween, lower, upper);

    /// <summary>Adds <c>field IS NULL</c>.</summary>
    public QueryRequestBuilder IsNull(string field) {
        this._filters.Add(new FilterConditionNode(RequireField(field), QueryOperator.IsNull));
        return this;
    }

    /// <summary>Adds <c>field IS NOT NULL</c>.</summary>
    public QueryRequestBuilder IsNotNull(string field) {
        this._filters.Add(new FilterConditionNode(RequireField(field), QueryOperator.IsNotNull));
        return this;
    }

    /// <summary>Sorts ascending by <paramref name="field"/>, after any sort already added.</summary>
    public QueryRequestBuilder OrderBy(string field) {
        this._sort.Add(new SortNode(RequireField(field), SortDirection.Ascending));
        return this;
    }

    /// <summary>Sorts descending by <paramref name="field"/>, after any sort already added.</summary>
    public QueryRequestBuilder OrderByDescending(string field) {
        this._sort.Add(new SortNode(RequireField(field), SortDirection.Descending));
        return this;
    }

    /// <summary>Sets the free-text search term, replacing any previous one.</summary>
    public QueryRequestBuilder Search(string? term) {
        this._search = new Q(term);
        return this;
    }

    /// <summary>Produces the request.</summary>
    public QueryRequest Build() {
        return new QueryRequest(
            this._search,
            this._sort.Count == 0 ? Sort.Empty : new Sort([.. this._sort]),
            [.. this._filters]);
    }

    private QueryRequestBuilder Add(string field, QueryOperator op, object? value) {
        this._filters.Add(new FilterConditionNode(RequireField(field), op, Render(value)));
        return this;
    }

    private QueryRequestBuilder AddList<TValue>(string field, QueryOperator op, IEnumerable<TValue> values) {
        string name = RequireField(field);
        Preca.ThrowIfNull(values);

        List<string> rendered = [];

        foreach(TValue value in values) {
            string item = Render(value);

            if(item.Contains(QuerySyntax.Comma)) {
                throw new ArgumentException(
                    $"The value '{item}' for '{name}' contains '{QuerySyntax.Comma}'. An in-list is split on commas with " +
                    "no escaping, so it would arrive as more than one value. Filter on it with Equal instead.",
                    nameof(values));
            }

            rendered.Add(item);
        }

        if(rendered.Count == 0) {
            throw new ArgumentException(
                $"The value list for '{name}' is empty. An empty in-list is ignored when the query is applied, so it " +
                "would match every row. Check for an empty set before building the query.",
                nameof(values));
        }

        this._filters.Add(new FilterConditionNode(name, op, string.Join(QuerySyntax.Comma, rendered)));
        return this;
    }

    private QueryRequestBuilder AddRange(string field, QueryOperator op, object lower, object upper) {
        string name = RequireField(field);
        string from = Render(RequireValue(lower));
        string to = Render(RequireValue(upper));

        if(from.Contains(QuerySyntax.RangeDelimiter, StringComparison.Ordinal) ||
           to.Contains(QuerySyntax.RangeDelimiter, StringComparison.Ordinal)) {
            throw new ArgumentException(
                $"A bound for '{name}' contains the range delimiter '{QuerySyntax.RangeDelimiter}'.",
                nameof(lower));
        }

        this._filters.Add(new FilterConditionNode(name, op, $"{from}{QuerySyntax.RangeDelimiter}{to}"));
        return this;
    }

    /// <summary>
    /// Renders a value the way the receiving side parses it: invariant culture, and round-trip formats for
    /// dates, which the invariant general format would otherwise truncate to the second.
    /// </summary>
    private static string Render(object? value) {
        return value switch {
            null => string.Empty,
            string text => text,
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string RequireField(string field) {
        Preca.ThrowIfNullOrWhiteSpace(field);
        return field.Trim();
    }

    private static T RequireValue<T>(T value) {
        Preca.ThrowIfNull(value);
        return value;
    }
}
