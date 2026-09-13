using System.Linq.Expressions;
using Wiaoj.Pagination;
using Wiaoj.Preconditions;
using Wiaoj.Querying;
using Wiaoj.Querying.Extensions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Applies a query contract — filters, search, sort and projection — and pages the result, in one call.
/// </summary>
/// <remarks>
/// Applying the query and paging it were separate calls that knew nothing of each other: the projection had to be
/// written between them, the tie-breaker remembered, and the metadata carried across a mapping step by hand. Each was
/// a place for a page to come back wrong without an error. Here the schema supplies all of it.
/// </remarks>
public static class QueryPaginationExtensions {

    /// <summary>
    /// Validates and applies <paramref name="query"/> through <paramref name="schema"/>, projects each row to
    /// <typeparamref name="TResponse"/>, and returns one offset page.
    /// </summary>
    /// <typeparam name="TEntity">The entity queried.</typeparam>
    /// <typeparam name="TResponse">The shape each row is returned in.</typeparam>
    /// <param name="source">The entity query, before any ordering.</param>
    /// <param name="query">The caller's filters, search and sort.</param>
    /// <param name="schema">The endpoint's query contract, carrying the projection and the tie-breaker.</param>
    /// <param name="page">The page to return.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the query.</param>
    /// <returns>The page, already in its response shape.</returns>
    /// <exception cref="QueryValidationException">The query does not satisfy the schema.</exception>
    /// <exception cref="InvalidOperationException">The schema declares no tie-breaker, or its contract does not hold.</exception>
    /// <remarks>
    /// <para>
    /// The query is validated, not merely applied. <c>ApplyQuery</c> skips a filter it does not recognise, which widens
    /// the result; behind the HTTP validation filter that cannot happen, but this call does not assume it is behind one.
    /// </para>
    /// <para>
    /// The ordering is the caller's sort, or the schema's default sort, followed by the schema's tie-breaker. Without
    /// the tie-breaker, rows tied on a repeated sort value could appear on two pages and on none. An ordering applied
    /// to <paramref name="source"/> before this call is replaced; the contract decides the order.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// PagedResult&lt;AssetSummaryResponse&gt; page = await db.Assets
    ///     .Where(a =&gt; a.ApplicationId == appId)
    ///     .ToPagedResultAsync(query, schema, pageRequest, ct);
    /// </code>
    /// </example>
    public static Task<PagedResult<TResponse>> ToPagedResultAsync<TEntity, TResponse>(
        this IQueryable<TEntity> source,
        QueryRequest query,
        QuerySchema<TEntity, TResponse> schema,
        PageRequest page,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(schema);

        LambdaExpression tieBreaker = RequireTieBreaker(schema);
        schema.VerifyContract();

        IQueryable<TEntity> ordered = AppendOrdering(source.ApplyValidatedQuery(query, schema), tieBreaker);

        return ordered.Select(schema.Projection).ToPagedResultAsync(page, cancellationToken);
    }

    private static LambdaExpression RequireTieBreaker<TEntity>(QuerySchema<TEntity> schema) {
        return schema.TieBreakerSelector ?? throw new InvalidOperationException(
            $"{schema.GetType().Name} declares no tie-breaker. Paging needs a unique key ordered last: rows that tie on " +
            "every requested sort field are otherwise returned in no fixed order, and can repeat on one page and be " +
            "missing from the next. Declare it with TieBreaker(e => e.Id).");
    }

    /// <summary>Orders by the tie-breaker after whatever ordering the query applied, or alone when it applied none.</summary>
    private static IQueryable<TEntity> AppendOrdering<TEntity>(IQueryable<TEntity> query, LambdaExpression tieBreaker) {
        string method = IsOrdered(query.Expression) ? nameof(Queryable.ThenBy) : nameof(Queryable.OrderBy);

        return query.Provider.CreateQuery<TEntity>(Expression.Call(
            typeof(Queryable), method, [typeof(TEntity), tieBreaker.ReturnType], query.Expression, Expression.Quote(tieBreaker)));
    }

    /// <summary>
    /// Whether the outermost call orders the query. <c>ApplyQuery</c> sorts last, so a sort it applied is outermost.
    /// </summary>
    private static bool IsOrdered(Expression expression) {
        return expression is MethodCallExpression {
            Method.DeclaringType: { } declaring,
            Method.Name: nameof(Queryable.OrderBy) or nameof(Queryable.OrderByDescending) or nameof(Queryable.ThenBy) or nameof(Queryable.ThenByDescending)
        } && declaring == typeof(Queryable);
    }
}
