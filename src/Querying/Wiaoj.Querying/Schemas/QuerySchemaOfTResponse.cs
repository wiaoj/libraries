using System.Linq.Expressions;
using System.Text;
using Wiaoj.Preconditions;

namespace Wiaoj.Querying;

/// <summary>
/// An endpoint's query contract: what may be filtered and sorted over <typeparamref name="TEntity"/>, and the
/// <typeparamref name="TResponse"/> each row comes back as.
/// </summary>
/// <typeparam name="TEntity">The entity queried.</typeparam>
/// <typeparam name="TResponse">The shape each row is returned in.</typeparam>
/// <remarks>
/// <para>
/// A filter surface is part of what an endpoint promises, and two endpoints over one entity can promise different
/// things. Binding the response to the schema makes the schema that promise in full, and lets its parts check each
/// other when the container first hands it out — rather than on the request that happens to exercise the gap.
/// </para>
/// <para>
/// A filterable or sortable field the projection never reads is refused. Filtering on data the caller cannot see is
/// usually a leak — narrowing a public listing by an owner id the response does not carry reveals which rows belong
/// to whom — and when it is intended, <see cref="PropertyRuleBuilder{T, TProperty}.NotInResponse"/> says so. Custom
/// filters have no column and are exempt.
/// </para>
/// <para>
/// A field counts as read when its member path appears anywhere in the projection: <c>a.Id.Encode()</c> reads
/// <c>Id</c>, <c>a.Owner</c> reads <c>Owner.Name</c>, and a positional record's constructor arguments are read like
/// any other expression.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class PublicAssetSchema : QuerySchema&lt;Asset, AssetSummaryResponse&gt; {
///     public PublicAssetSchema() {
///         Project(a =&gt; new AssetSummaryResponse(a.Id.Encode(), a.FileName, a.FileSize));
///         AllowFilter(a =&gt; a.FileName);
///         AllowSort(a =&gt; a.FileSize);
///     }
/// }
/// </code>
/// </example>
public class QuerySchema<TEntity, TResponse> : QuerySchema<TEntity>, IQuerySchemaContract {
    private Expression<Func<TEntity, TResponse>>? _projection;

    /// <summary>
    /// Gets the projection from <typeparamref name="TEntity"/> to <typeparamref name="TResponse"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Project"/> was never called.</exception>
    public Expression<Func<TEntity, TResponse>> Projection => this._projection
        ?? throw new InvalidOperationException(
            $"{this.GetType().Name} declares no projection to {typeof(TResponse).Name}. Call Project(...) in its constructor.");

    /// <summary>
    /// Sets how each <typeparamref name="TEntity"/> becomes a <typeparamref name="TResponse"/>.
    /// </summary>
    /// <param name="projection">
    /// A projection the query provider can translate. A non-translatable call such as an id's <c>Encode()</c> is
    /// allowed where the provider evaluates the final projection on the client, as EF Core does.
    /// </param>
    /// <returns>The schema, for chaining.</returns>
    public QuerySchema<TEntity, TResponse> Project(Expression<Func<TEntity, TResponse>> projection) {
        Preca.ThrowIfNull(projection);
        this._projection = projection;
        return this;
    }

    /// <summary>
    /// Checks the schema against its projection.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No projection was set, or a filterable or sortable field is not read by it and not marked
    /// <see cref="PropertyRuleBuilder{T, TProperty}.NotInResponse"/>.
    /// </exception>
    /// <remarks>
    /// Runs when the container first hands the schema out. Call it directly for a schema constructed by hand.
    /// </remarks>
    public void VerifyContract() {
        Expression<Func<TEntity, TResponse>> projection = this.Projection;
        HashSet<string> read = ReadMemberPaths(projection);

        List<string> unread = [];
        foreach(QueryProperty<TEntity> property in this.Properties) {
            if(property.IsCustom || property.IsNotInResponse || !(property.IsFilterable || property.IsSortable)) {
                continue;
            }

            if(!IsRead(property.MemberName, read)) {
                unread.Add(property.ExposedName);
            }
        }

        if(unread.Count > 0) {
            unread.Sort(StringComparer.Ordinal);
            throw new InvalidOperationException(
                $"{this.GetType().Name} lets callers filter or sort by {string.Join(", ", unread.Select(name => $"'{name}'"))}, " +
                $"which its projection to {typeof(TResponse).Name} never returns. Filtering on data the caller cannot see " +
                "can reveal it one narrowed result at a time. Return the field, stop allowing it, or mark it " +
                "Property(...).NotInResponse() if filtering on it is intended.");
        }
    }

    /// <summary>A path counts as read when it, an ancestor of it, or a descendant of it appears in the projection.</summary>
    private static bool IsRead(string memberPath, HashSet<string> read) {
        foreach(string path in read) {
            if(string.Equals(path, memberPath, StringComparison.Ordinal)
               || memberPath.StartsWith(path + ".", StringComparison.Ordinal)
               || path.StartsWith(memberPath + ".", StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> ReadMemberPaths(LambdaExpression projection) {
        MemberPathCollector collector = new(projection.Parameters[0]);
        collector.Visit(projection.Body);
        return collector.Paths;
    }

    /// <summary>Collects every maximal member access chain rooted at the projection's parameter.</summary>
    private sealed class MemberPathCollector(ParameterExpression parameter) : ExpressionVisitor {
        public HashSet<string> Paths { get; } = new(StringComparer.Ordinal);

        protected override Expression VisitMember(MemberExpression node) {
            if(TryPath(node, out string? path)) {
                this.Paths.Add(path);
                return node;
            }

            return base.VisitMember(node);
        }

        private bool TryPath(MemberExpression node, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? path) {
            Stack<string> segments = new();
            Expression? current = node;

            while(current is MemberExpression member) {
                segments.Push(member.Member.Name);
                current = member.Expression;
                while(current is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary) {
                    current = unary.Operand;
                }
            }

            if(current != parameter) {
                path = null;
                return false;
            }

            StringBuilder builder = new();
            while(segments.Count > 0) {
                builder.Append(segments.Pop());
                if(segments.Count > 0) {
                    builder.Append('.');
                }
            }

            path = builder.ToString();
            return true;
        }
    }
}

/// <summary>
/// A schema whose parts can be checked against each other once it is fully configured.
/// </summary>
internal interface IQuerySchemaContract {
    void VerifyContract();
}
