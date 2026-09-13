using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static partial class QueryablePaginationExtensions {

    /// <summary>
    /// Checks that the queryable is ordered by exactly the cursor keys, and returns each key's direction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The seek predicate is built from the key selectors; the page window comes from the queryable's own ordering.
    /// When the two describe different columns, each page is cut from one ordering and continued in another, so rows
    /// are skipped and repeated and every call still succeeds (#73). The combination is reachable from a query
    /// string, where the client chooses the <c>ORDER BY</c> and the handler fixes the cursor key, which makes a
    /// silent answer unacceptable. Every disagreement throws, on the first page as on every other.
    /// </para>
    /// <para>
    /// Only the effective ordering is read: walking inward from the outermost call, <c>ThenBy</c> levels are collected
    /// until the <c>OrderBy</c> that starts the chain, and an <c>OrderBy</c> further in has been replaced and is
    /// ignored. Calls that keep the element type — <c>Where</c>, <c>AsNoTracking</c>, <c>Include</c> — are passed
    /// through. A <c>Select</c> is seen through when it is an anonymous type or a member initialiser, so a key such as
    /// <c>x =&gt; x.Key</c> over <c>new { Key = a.Id }</c> is compared as <c>a.Id</c>. A constructor call binds
    /// nothing that can be followed, so a key reached through one cannot be verified and is refused.
    /// </para>
    /// <para>
    /// Trailing keys with no ordering level — the <c>Id</c> tie-breaker injected by the built-in overloads is the
    /// usual one — take the direction of the level before them, and are added to the <c>ORDER BY</c> by
    /// <see cref="ApplyKeysetOrdering"/>. Without that, tied rows came back in whatever order the database chose while
    /// the seek assumed the tie-breaker's.
    /// </para>
    /// </remarks>
    /// <param name="expression">The queryable's expression tree.</param>
    /// <param name="keys">The key selectors the seek predicate is built from, in significance order.</param>
    /// <returns>Each key's direction: <see langword="true"/> for descending.</returns>
    /// <exception cref="InvalidOperationException">
    /// The query is unordered, has more ordering levels than keys, is ordered by something other than a key at any
    /// level, or reaches a key through a projection that cannot be followed.
    /// </exception>
    private static bool[] VerifyKeysetOrdering(Expression expression, params LambdaExpression[] keys) {
        ParameterExpression parameter = keys[0].Parameters[0];
        Expression?[] keyBodies = new Expression?[keys.Length];
        for(int i = 0; i < keys.Length; i++) {
            keyBodies[i] = ParameterReplacer.Replace(keys[i].Body, keys[i].Parameters[0], parameter);
        }

        // Outermost first. Each level records the keys as they read at its own position in the chain, because a
        // projection between the ordering and the call changes what the key selectors refer to.
        List<(LambdaExpression Ordering, Expression Body, Expression?[] Keys, bool Descending)> levels = [];

        Expression? current = expression;
        while(current is MethodCallExpression call && call.Arguments.Count > 0) {
            if(call.Method.DeclaringType == typeof(Queryable)) {
                string name = call.Method.Name;

                if(name is nameof(Queryable.OrderBy) or nameof(Queryable.OrderByDescending)
                        or nameof(Queryable.ThenBy) or nameof(Queryable.ThenByDescending)
                    && call.Arguments.Count == 2) {

                    LambdaExpression ordering = Unquote(call.Arguments[1]);
                    Expression body = ParameterReplacer.Replace(ordering.Body, ordering.Parameters[0], parameter);
                    bool descending = name is nameof(Queryable.OrderByDescending) or nameof(Queryable.ThenByDescending);

                    levels.Add((ordering, body, (Expression?[])keyBodies.Clone(), descending));

                    if(name is nameof(Queryable.OrderBy) or nameof(Queryable.OrderByDescending)) {
                        break;
                    }

                    current = call.Arguments[0];
                    continue;
                }

                if(name is nameof(Queryable.Select) && call.Arguments.Count == 2
                    && Unquote(call.Arguments[1]) is { Parameters.Count: 1 } projection) {

                    for(int i = 0; i < keyBodies.Length; i++) {
                        keyBodies[i] = keyBodies[i] is { } keyBody
                            ? ProjectionInliner.TryInline(keyBody, parameter, projection)
                            : null;
                    }

                    parameter = projection.Parameters[0];
                    current = call.Arguments[0];
                    continue;
                }
            }

            // Anything else that keeps the element type is transparent to ordering. A call that changes it without a
            // Select to follow — GroupBy, SelectMany, Join — ends the chain: an ordering before it is not this one.
            if(ElementTypeOf(call.Arguments[0].Type) == parameter.Type) {
                current = call.Arguments[0];
                continue;
            }

            break;
        }

        if(levels.Count == 0) {
            throw new InvalidOperationException(
                $"Keyset pagination needs the query ordered by its cursor key, and this query is not ordered. Without " +
                $"ORDER BY the database may return rows in any order, while the seek predicate assumes one. Order it " +
                $"by {Describe(keys)} before calling ToCursorResultAsync.");
        }

        if(levels.Count > keys.Length) {
            throw new InvalidOperationException(
                $"The query has {levels.Count} ordering levels, but the cursor was given {keys.Length} key(s): " +
                $"{Describe(keys)}. Ordering levels without a key are not part of the seek predicate, so rows tied on " +
                "the keys would be ordered by one thing and continued by another, and be skipped or repeated across " +
                "pages. Pass every ordering level as a key, including the tie-breaker, or remove the extra levels.");
        }

        levels.Reverse(); // Now in application order: OrderBy first.

        bool[] directions = new bool[keys.Length];
        for(int i = 0; i < keys.Length; i++) {
            if(i >= levels.Count) {
                directions[i] = directions[i - 1];
                continue;
            }

            (LambdaExpression ordering, Expression body, Expression?[] keysAtLevel, bool descending) = levels[i];

            if(keysAtLevel[i] is null) {
                throw new InvalidOperationException(
                    $"The cursor key {keys[i].Body} is read from a projection that cannot be traced back to the column " +
                    $"the query is ordered by ({ordering.Body}), so the two cannot be checked to agree. Only anonymous " +
                    "types and member initialisers (new T { Key = x.Id }) can be followed; a constructor call cannot. " +
                    "Project with a member initialiser, or order after the projection.");
            }

            if(!ExpressionEqualityComparer.Instance.Equals(body, keysAtLevel[i])) {
                throw new InvalidOperationException(
                    $"The query is ordered by {ordering.Body} at level {i + 1}, but the cursor seeks on {keys[i].Body}. " +
                    "Each page would be cut from one ordering and continued in the other, so rows would be skipped and " +
                    "repeated across pages without any error. Order by the cursor key, or page on the column the query " +
                    "is ordered by. If the ordering comes from a client-chosen sort (such as Wiaoj.Querying's ?sort=), " +
                    "restrict AllowSort on this endpoint to the cursor key.");
            }

            directions[i] = descending;
        }

        return directions;
    }

    /// <summary>
    /// Orders the query by the cursor keys, in the verified directions or reversed for a backward page.
    /// </summary>
    /// <remarks>
    /// Applied to every page, not only backward ones. The keys were verified to be the query's own ordering, so this
    /// replaces it with itself — and adds any trailing key the caller did not write, which the seek relies on.
    /// </remarks>
    private static IQueryable<TSource> ApplyKeysetOrdering<TSource>(
        IQueryable<TSource> query,
        bool[] descending,
        bool reverse,
        params LambdaExpression[] keys) {

        Expression expression = query.Expression;
        for(int i = 0; i < keys.Length; i++) {
            bool isDescending = descending[i] != reverse;
            string method = (i == 0, isDescending) switch {
                (true, false) => nameof(Queryable.OrderBy),
                (true, true) => nameof(Queryable.OrderByDescending),
                (false, false) => nameof(Queryable.ThenBy),
                (false, true) => nameof(Queryable.ThenByDescending)
            };

            expression = Expression.Call(
                typeof(Queryable), method, [typeof(TSource), keys[i].ReturnType], expression, Expression.Quote(keys[i]));
        }

        return query.Provider.CreateQuery<TSource>(expression);
    }

    private static LambdaExpression Unquote(Expression expression) {
        while(expression is UnaryExpression { NodeType: ExpressionType.Quote } quote) {
            expression = quote.Operand;
        }

        return (LambdaExpression)expression;
    }

    private static Type? ElementTypeOf(Type queryableType) {
        return queryableType.IsGenericType && typeof(IQueryable).IsAssignableFrom(queryableType)
            ? queryableType.GetGenericArguments()[0]
            : queryableType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>))?
                .GetGenericArguments()[0];
    }

    private static string Describe(LambdaExpression[] keys) {
        return string.Join(", then ", keys.Select(key => key.ToString()));
    }

    /// <summary>
    /// Rewrites an expression over a projection's result into the same expression over the projection's source.
    /// </summary>
    private sealed class ProjectionInliner : ExpressionVisitor {
        private readonly ParameterExpression _projected;
        private readonly LambdaExpression _projection;
        private bool _failed;

        private ProjectionInliner(ParameterExpression projected, LambdaExpression projection) {
            this._projected = projected;
            this._projection = projection;
        }

        /// <summary>Returns the rewritten expression, or <see langword="null"/> when a member cannot be followed.</summary>
        public static Expression? TryInline(Expression body, ParameterExpression projected, LambdaExpression projection) {
            ProjectionInliner inliner = new(projected, projection);
            Expression rewritten = inliner.Visit(body);
            return inliner._failed ? null : rewritten;
        }

        protected override Expression VisitMember(MemberExpression node) {
            if(node.Expression != this._projected || this._projection.Body == this._projection.Parameters[0]) {
                return base.VisitMember(node);
            }

            Expression? bound = this._projection.Body switch {
                NewExpression { Members: { } members } created => FindArgument(created, members, node.Member.Name),
                MemberInitExpression init => init.Bindings
                    .OfType<MemberAssignment>()
                    .FirstOrDefault(binding => binding.Member.Name == node.Member.Name)?
                    .Expression,
                _ => null
            };

            if(bound is null) {
                this._failed = true;
                return node;
            }

            return bound;
        }

        protected override Expression VisitParameter(ParameterExpression node) {
            if(node != this._projected) {
                return node;
            }

            // An identity projection passes the element through; anything else used whole cannot be followed.
            if(this._projection.Body == this._projection.Parameters[0]) {
                return this._projection.Parameters[0];
            }

            this._failed = true;
            return node;
        }

        private static Expression? FindArgument(NewExpression created, IReadOnlyList<MemberInfo> members, string name) {
            for(int i = 0; i < members.Count; i++) {
                string member = members[i].Name.StartsWith("get_", StringComparison.Ordinal)
                    ? members[i].Name[4..]
                    : members[i].Name;

                if(member == name) {
                    return created.Arguments[i];
                }
            }

            return null;
        }
    }
}
