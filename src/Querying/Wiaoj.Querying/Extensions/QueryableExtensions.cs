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
    public static IQueryable<T> ApplyQuery<T>(
        this IQueryable<T> query,
        QueryRequest request,
        QuerySchema<T> schema) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(schema);

        if(request.IsEmpty) {
            return query;
        }

        ParameterExpression parameter = Expression.Parameter(typeof(T), "x");

        // 1. Apply free-text search predicate (Q)
        Expression<Func<T, bool>>? searchPredicate = FilterExpressionBuilder.BuildSearchPredicate(request.Q, schema, parameter);
        if(searchPredicate != null) {
            query = query.Where(searchPredicate);
        }

        // 2. Apply filter conditions predicate
        Expression<Func<T, bool>>? filterPredicate = FilterExpressionBuilder.BuildFilterPredicate(request.Filters, schema, parameter);
        if(filterPredicate != null) {
            query = query.Where(filterPredicate);
        }

        // 3. Apply sorting directly using Sort nodes without string allocations
        if(!request.Sort.IsEmpty) {
            query = ApplySorting(query, request.Sort, schema);
        }

        return query;
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
}