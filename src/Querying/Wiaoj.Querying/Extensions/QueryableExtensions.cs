using System.Linq.Expressions;
using Wiaoj.Querying.Expressions;

namespace Wiaoj.Querying.Extensions;

/// <summary>
/// Provides extension methods for applying structured query filtering, search, and sorting to <see cref="IQueryable{T}"/>.
/// </summary>
public static class QueryableExtensions {
    /// <summary>
    /// Applies search terms, filter conditions, and sort criteria from a <see cref="QueryRequest"/>
    /// onto the target <see cref="IQueryable{T}"/> using the security and rule definitions from <see cref="QuerySchema{T}"/>.
    /// </summary>
    /// <typeparam name="T">The entity type of the data source.</typeparam>
    /// <param name="query">The underlying queryable source to extend.</param>
    /// <param name="request">The query request containing free-text search, filter condition nodes, and sorting directives.</param>
    /// <param name="schema">The entity query schema that defines allowed properties, permitted operators, and safety limits.</param>
    /// <returns>A new <see cref="IQueryable{T}"/> instance with all validated filter, search, and sort expressions applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> or <paramref name="schema"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Application order:
    /// <list type="number">
    /// <item><description><see cref="QuerySchema{T}.RequireFilter"/> predicates — always applied, even for an entirely empty request.</description></item>
    /// <item><description>Free-text search (<c>q=</c>) and the caller's own filter conditions — skipped when the request is empty.</description></item>
    /// <item><description><see cref="QuerySchema{T}.DefaultFilter{TProperty}"/> predicates — applied per field, but only for fields the caller did not explicitly filter.</description></item>
    /// <item><description>Sorting — the caller's <c>sort</c> if present; otherwise <see cref="QuerySchema{T}.DefaultSort{TProperty}"/>.</description></item>
    /// </list>
    /// </remarks>
    public static IQueryable<T> ApplyQuery<T>(
        this IQueryable<T> query,
        QueryRequest request,
        QuerySchema<T> schema) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schema);

        // 1. Required (locked) filters always apply, regardless of request content — including an entirely
        //    empty request, since these are schema-level invariants, not something the caller opted into.
        query = ApplyRequiredFilters(query, schema);

        if(!request.IsEmpty) {
            ParameterExpression parameter = Expression.Parameter(typeof(T), "x");

            // 2. Apply free-text search predicate (Q)
            Expression<Func<T, bool>>? searchPredicate = FilterExpressionBuilder.BuildSearchPredicate(request.Q, schema, parameter);
            if(searchPredicate != null) {
                query = query.Where(searchPredicate);
            }

            // 3. Apply the caller's own filter conditions
            Expression<Func<T, bool>>? filterPredicate = FilterExpressionBuilder.BuildFilterPredicate(request.Filters, schema, parameter);
            if(filterPredicate != null) {
                query = query.Where(filterPredicate);
            }
        }

        // 4. Default filters: applied per field, but only for fields the caller didn't explicitly filter.
        //    Runs regardless of request.IsEmpty, since an empty request is exactly the case these exist for.
        query = ApplyDefaultFilters(query, request.Filters, schema);

        // 5. Sorting: the caller's own sort takes priority; otherwise fall back to the schema's default sort.
        query = !request.Sort.IsEmpty
            ? ApplySorting(query, request.Sort, schema)
            : ApplyDefaultSort(query, schema);

        return query;
    }

    private static IQueryable<T> ApplyRequiredFilters<T>(IQueryable<T> query, QuerySchema<T> schema) {
        IReadOnlyList<Expression<Func<T, bool>>> required = schema.RequiredFilters;
        for(int i = 0; i < required.Count; i++) {
            query = query.Where(required[i]);
        }

        return query;
    }

    private static IQueryable<T> ApplyDefaultFilters<T>(
        IQueryable<T> query,
        IReadOnlyList<FilterConditionNode> userFilters,
        QuerySchema<T> schema) {
        IReadOnlyList<(string MemberPath, Expression<Func<T, bool>> Predicate)> defaults = schema.DefaultFilterRules;
        if(defaults.Count == 0) {
            return query;
        }

        for(int i = 0; i < defaults.Count; i++) {
            (string memberPath, Expression<Func<T, bool>> predicate) = defaults[i];
            string exposedName = schema.ResolveExposedName(memberPath);

            if(!IsFieldExplicitlyFiltered(userFilters, exposedName)) {
                query = query.Where(predicate);
            }
        }

        return query;
    }

    private static bool IsFieldExplicitlyFiltered(IReadOnlyList<FilterConditionNode> userFilters, string exposedFieldName) {
        for(int i = 0; i < userFilters.Count; i++) {
            if(string.Equals(userFilters[i].Field, exposedFieldName, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static IQueryable<T> ApplySorting<T>(
        IQueryable<T> query,
        Sort sort,
        QuerySchema<T> schema) {
        bool isFirstSort = true;
        int appliedSorts = 0;

        for(int i = 0; i < sort.Count; i++) {
            if(appliedSorts >= schema.MaxSortFieldsCount) {
                break;
            }

            SortNode node = sort[i];

            if(!schema.TryGetProperty(node.Field, out QueryProperty<T>? prop) ||
               !prop.IsSortable ||
               !schema.IsSortAllowed(node.Field) ||
               prop.SortApplier == null) {
                continue;
            }

            query = prop.SortApplier(query, node.IsDescending, isFirstSort);
            isFirstSort = false;
            appliedSorts++;
        }

        return query;
    }

    private static IQueryable<T> ApplyDefaultSort<T>(IQueryable<T> query, QuerySchema<T> schema) {
        IReadOnlyList<Func<IQueryable<T>, bool, IQueryable<T>>> appliers = schema.DefaultSortAppliers;
        bool isFirstSort = true;

        for(int i = 0; i < appliers.Count; i++) {
            query = appliers[i](query, isFirstSort);
            isFirstSort = false;
        }

        return query;
    }
}