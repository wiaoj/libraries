using System.Buffers.Binary;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Wiaoj.Pagination;
using Wiaoj.Preconditions;
using Wiaoj.Querying;
using Wiaoj.Querying.Extensions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Applies a query contract and returns one keyset page, seeking on the sort the caller chose.
/// </summary>
/// <remarks>
/// <para>
/// Applying a client-chosen sort and paging with a cursor fixed on another key computes the page window and the seek
/// window over different columns, and rows are skipped and repeated without an error (#73). The guard added for that
/// refuses the combination. This makes the two compose instead: the call knows the sort, so the cursor is built from
/// the sort keys followed by the tie-breaker, and the seek continues exactly where the ordering left off.
/// </para>
/// <para>
/// A cursor records which sort it was issued for. Sent back with a different <c>sort</c>, it would continue one
/// ordering in another — the same failure by a different route — so it is refused with a validation error.
/// </para>
/// </remarks>
public static class QueryKeysetExtensions {
    private const byte FormatVersion = 1;

    /// <summary>
    /// Validates and applies <paramref name="query"/> through <paramref name="schema"/>, projects each row to
    /// <typeparamref name="TResponse"/>, and returns the keyset page <paramref name="request"/> points at.
    /// </summary>
    /// <typeparam name="TEntity">The entity queried.</typeparam>
    /// <typeparam name="TResponse">The shape each row is returned in.</typeparam>
    /// <param name="source">The entity query, before any ordering.</param>
    /// <param name="query">The caller's filters, search and sort.</param>
    /// <param name="schema">The endpoint's query contract: projection, cursor keys and tie-breaker.</param>
    /// <param name="request">The cursor, limit and direction.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the query.</param>
    /// <returns>The page, already in its response shape, with cursors for the pages either side.</returns>
    /// <exception cref="QueryValidationException">
    /// The query does not satisfy the schema; it sorts by a field not declared <c>AsCursor()</c>; or the cursor is
    /// unreadable or was issued for a different sort. All are the caller's to fix.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The schema cannot page with a cursor — no tie-breaker, no codec for it, a default sort field not declared
    /// <c>AsCursor()</c> — or a row on a page boundary has a null key.
    /// </exception>
    /// <remarks>
    /// The ordering is the caller's sort, or the schema's default sort, followed by the tie-breaker; with neither sort,
    /// the tie-breaker alone. An ordering applied to <paramref name="source"/> before this call is replaced.
    /// </remarks>
    /// <example>
    /// <code>
    /// CursorResult&lt;AssetSummaryResponse&gt; page = await db.Assets
    ///     .Where(a =&gt; a.ApplicationId == appId)
    ///     .ToCursorResultAsync(query, schema, cursorRequest, ct);
    /// </code>
    /// </example>
    public static async Task<CursorResult<TResponse>> ToCursorResultAsync<TEntity, TResponse>(
        this IQueryable<TEntity> source,
        QueryRequest query,
        QuerySchema<TEntity, TResponse> schema,
        CursorRequest request,
        CancellationToken cancellationToken = default) {

        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(schema);

        schema.VerifyContract();
        IReadOnlyList<QueryCursorKey> keys = schema.ResolveCursorKeys(query.Sort);
        IQueryable<TEntity> filtered = source.ApplyValidatedQuery(query, schema);

        ParameterExpression entity = Expression.Parameter(typeof(TEntity), "e");
        Expression[] keyBodies = [.. keys.Select(key => Rebind(key.Selector, entity))];

        bool seeking = !request.Cursor.IsEmpty;
        bool forward = request.Direction == CursorDirection.Forward;
        ulong fingerprint = Fingerprint(keys);

        if(seeking) {
            object[] pivot = ReadCursor(request.Cursor, fingerprint, keys);
            filtered = filtered.Where(Expression.Lambda<Func<TEntity, bool>>(SeekPredicate(keys, keyBodies, pivot, forward), entity));
        }

        IQueryable<TEntity> ordered = Order(filtered, keys, keyBodies, entity, reverse: seeking && !forward);

        List<KeysetRow<TResponse>> rows = await ordered
            .Take(request.Limit + 1)
            .Select(RowProjection(schema.Projection, keyBodies, entity))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if(rows.Count == 0) {
            return CursorResult<TResponse>.Empty;
        }

        bool hasMore = rows.Count > request.Limit;
        if(hasMore) {
            rows.RemoveAt(rows.Count - 1);
        }

        if(seeking && !forward) {
            rows.Reverse();
        }

        CursorMetadata metadata = new(
            WriteCursor(rows[0].Keys, fingerprint, keys),
            WriteCursor(rows[^1].Keys, fingerprint, keys),
            hasPrevious: forward ? seeking : hasMore,
            hasNext: forward ? hasMore : seeking);

        return new CursorResult<TResponse>([.. rows.Select(row => row.Item)], metadata);
    }

    /// <summary>A projected row and the key values its cursor is written from, read in the same query.</summary>
    internal sealed class KeysetRow<TResponse> {
        public TResponse Item { get; set; } = default!;

        public object?[] Keys { get; set; } = [];
    }

    private static Expression<Func<TEntity, KeysetRow<TResponse>>> RowProjection<TEntity, TResponse>(
        Expression<Func<TEntity, TResponse>> projection,
        Expression[] keyBodies,
        ParameterExpression entity) {

        MemberInitExpression body = Expression.MemberInit(
            Expression.New(typeof(KeysetRow<TResponse>)),
            Expression.Bind(typeof(KeysetRow<TResponse>).GetProperty(nameof(KeysetRow<TResponse>.Item))!, Rebind(projection, entity)),
            Expression.Bind(typeof(KeysetRow<TResponse>).GetProperty(nameof(KeysetRow<TResponse>.Keys))!,
                Expression.NewArrayInit(typeof(object), keyBodies.Select(body => Expression.Convert(body, typeof(object))))));

        return Expression.Lambda<Func<TEntity, KeysetRow<TResponse>>>(body, entity);
    }

    private static IQueryable<TEntity> Order<TEntity>(
        IQueryable<TEntity> query,
        IReadOnlyList<QueryCursorKey> keys,
        Expression[] keyBodies,
        ParameterExpression entity,
        bool reverse) {

        Expression expression = query.Expression;
        for(int i = 0; i < keys.Count; i++) {
            bool descending = keys[i].IsDescending != reverse;
            string method = (i == 0, descending) switch {
                (true, false) => nameof(Queryable.OrderBy),
                (true, true) => nameof(Queryable.OrderByDescending),
                (false, false) => nameof(Queryable.ThenBy),
                (false, true) => nameof(Queryable.ThenByDescending)
            };

            LambdaExpression selector = Expression.Lambda(keyBodies[i], entity);
            expression = Expression.Call(typeof(Queryable), method, [typeof(TEntity), keyBodies[i].Type], expression, Expression.Quote(selector));
        }

        return query.Provider.CreateQuery<TEntity>(expression);
    }

    /// <summary>
    /// <c>(k0 ▷ p0) OR (k0 = p0 AND k1 ▷ p1) OR …</c>, where ▷ is the direction each key continues in.
    /// </summary>
    private static Expression SeekPredicate(IReadOnlyList<QueryCursorKey> keys, Expression[] keyBodies, object[] pivot, bool forward) {
        Expression? predicate = null;
        Expression? equalSoFar = null;

        for(int i = 0; i < keys.Count; i++) {
            bool greaterThan = keys[i].IsDescending != forward;
            Expression step = Compare(keyBodies[i], pivot[i], greaterThan);
            Expression term = equalSoFar is null ? step : Expression.AndAlso(equalSoFar, step);

            predicate = predicate is null ? term : Expression.OrElse(predicate, term);

            Expression equal = EqualTo(keyBodies[i], pivot[i]);
            equalSoFar = equalSoFar is null ? equal : Expression.AndAlso(equalSoFar, equal);
        }

        return predicate!;
    }

    /// <summary>
    /// <c>key &gt; pivot</c> or <c>key &lt; pivot</c>, in the form each type translates correctly.
    /// </summary>
    /// <remarks>
    /// The same rules as the composite seeks in <c>Wiaoj.Pagination.EntityFrameworkCore</c>. <see cref="string"/> goes
    /// through <see cref="string.Compare(string, string)"/>; a type declaring relational operators uses them, since a
    /// provider may translate the operator form specially (SQLite compares <see cref="decimal"/> text through a function
    /// for it, but not for <c>CompareTo</c>); an enum compares as its underlying number; anything else goes through
    /// <see cref="IComparable{T}.CompareTo"/>, which EF Core reduces to a column comparison for value-converted keys.
    /// </remarks>
    private static Expression Compare(Expression key, object value, bool greaterThan) {
        Type type = key.Type;

        if(type == typeof(string)) {
            MethodInfo compare = typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;
            return Relational(Expression.Call(compare, key, Expression.Constant(value, type)), Expression.Constant(0), greaterThan);
        }

        if(type.IsEnum) {
            Type underlying = Enum.GetUnderlyingType(type);
            return Relational(Expression.Convert(key, underlying), Expression.Constant(Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.InvariantCulture), underlying), greaterThan);
        }

        if(type.IsPrimitive || DeclaresOperator(type, greaterThan ? "op_GreaterThan" : "op_LessThan")) {
            return Relational(key, Expression.Constant(value, type), greaterThan);
        }

        return Relational(CompareTo(key, value), Expression.Constant(0), greaterThan);
    }

    private static Expression EqualTo(Expression key, object value) {
        Type type = key.Type;

        if(type.IsEnum) {
            Type underlying = Enum.GetUnderlyingType(type);
            return Expression.Equal(Expression.Convert(key, underlying), Expression.Constant(Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.InvariantCulture), underlying));
        }

        if(type.IsPrimitive || type == typeof(string) || DeclaresOperator(type, "op_Equality")) {
            return Expression.Equal(key, Expression.Constant(value, type));
        }

        return Expression.Equal(CompareTo(key, value), Expression.Constant(0));
    }

    private static MethodCallExpression CompareTo(Expression key, object value) {
        Type comparable = typeof(IComparable<>).MakeGenericType(key.Type);

        if(!comparable.IsAssignableFrom(key.Type)) {
            throw new InvalidOperationException(
                $"The cursor key {key} is a {key.Type.Name}, which declares no relational operators and does not implement " +
                $"IComparable<{key.Type.Name}>, so a seek cannot compare it.");
        }

        return Expression.Call(key, comparable.GetMethod(nameof(IComparable<object>.CompareTo))!, Expression.Constant(value, key.Type));
    }

    private static BinaryExpression Relational(Expression left, Expression right, bool greaterThan) {
        return greaterThan ? Expression.GreaterThan(left, right) : Expression.LessThan(left, right);
    }

    private static bool DeclaresOperator(Type type, string name) {
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, [type, type]) is not null;
    }

    private static Expression Rebind(LambdaExpression lambda, ParameterExpression parameter) {
        return new ParameterRebinder(lambda.Parameters[0], parameter).Visit(lambda.Body);
    }

    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }

    /// <summary>
    /// Identifies the ordering a cursor belongs to: each key's name and direction, in order.
    /// </summary>
    /// <remarks>FNV-1a over the description. It detects a changed sort; it is not a signature against tampering.</remarks>
    private static ulong Fingerprint(IReadOnlyList<QueryCursorKey> keys) {
        string description = string.Join('|', keys.Select(key => $"{key.Name}:{(key.IsDescending ? 'd' : 'a')}"));

        ulong hash = 14695981039346656037UL;
        foreach(byte b in Encoding.UTF8.GetBytes(description)) {
            hash = (hash ^ b) * 1099511628211UL;
        }

        return hash;
    }

    /// <summary>Version, fingerprint, key count, then each key as a length-prefixed UTF-8 string.</summary>
    private static CursorToken WriteCursor(object?[] values, ulong fingerprint, IReadOnlyList<QueryCursorKey> keys) {
        byte[][] encoded = new byte[keys.Count][];
        for(int i = 0; i < keys.Count; i++) {
            object value = values[i] ?? throw new InvalidOperationException(
                $"The cursor key '{keys[i].Name}' is null on a page boundary. SQL compares NULL with nothing, so a seek from " +
                "this row would end the traversal early. A cursor key column must not contain nulls; filter them out or " +
                "sort by another field.");

            encoded[i] = Encoding.UTF8.GetBytes(keys[i].Codec.Encode(value));
        }

        byte[] buffer = new byte[1 + sizeof(ulong) + 1 + encoded.Sum(bytes => sizeof(int) + bytes.Length)];
        buffer[0] = FormatVersion;
        BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(1), fingerprint);
        buffer[1 + sizeof(ulong)] = (byte)keys.Count;

        int offset = 1 + sizeof(ulong) + 1;
        foreach(byte[] bytes in encoded) {
            BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(offset), bytes.Length);
            bytes.CopyTo(buffer, offset + sizeof(int));
            offset += sizeof(int) + bytes.Length;
        }

        return CursorToken.FromBytes(buffer);
    }

    private static object[] ReadCursor(CursorToken token, ulong fingerprint, IReadOnlyList<QueryCursorKey> keys) {
        byte[] buffer;
        try {
            buffer = token.ToBytes();
        }
        catch(FormatException) {
            throw CursorError(QueryValidationErrorCode.InvalidCursor, "The cursor is not a valid cursor.");
        }

        const int header = 1 + sizeof(ulong) + 1;
        if(buffer.Length < header || buffer[0] != FormatVersion) {
            throw CursorError(QueryValidationErrorCode.InvalidCursor, "The cursor is not a valid cursor.");
        }

        if(BinaryPrimitives.ReadUInt64BigEndian(buffer.AsSpan(1)) != fingerprint || buffer[1 + sizeof(ulong)] != keys.Count) {
            throw CursorError(QueryValidationErrorCode.CursorSortChanged,
                "The cursor was issued for a different sort. Continuing it under this sort would skip and repeat rows; " +
                "request the first page again without a cursor.");
        }

        object[] values = new object[keys.Count];
        int offset = header;
        for(int i = 0; i < keys.Count; i++) {
            if(offset + sizeof(int) > buffer.Length) {
                throw CursorError(QueryValidationErrorCode.InvalidCursor, "The cursor is not a valid cursor.");
            }

            int length = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(offset));
            offset += sizeof(int);

            if(length < 0 || offset + length > buffer.Length) {
                throw CursorError(QueryValidationErrorCode.InvalidCursor, "The cursor is not a valid cursor.");
            }

            try {
                values[i] = keys[i].Codec.Decode(Encoding.UTF8.GetString(buffer, offset, length));
            }
            catch(FormatException) {
                throw CursorError(QueryValidationErrorCode.InvalidCursor, "The cursor is not a valid cursor.");
            }

            offset += length;
        }

        if(offset != buffer.Length) {
            throw CursorError(QueryValidationErrorCode.InvalidCursor, "The cursor is not a valid cursor.");
        }

        return values;
    }

    private static QueryValidationException CursorError(QueryValidationErrorCode code, string message) {
        return new QueryValidationException(new QueryValidationResult([new QueryValidationError(PaginationParameters.Cursor, code, message)]));
    }
}
