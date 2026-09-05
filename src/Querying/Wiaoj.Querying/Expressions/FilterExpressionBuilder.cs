using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Wiaoj.Querying.Expressions;

/// <summary>
/// Compiles query conditions, search terms, and operator bitmasks into LINQ expression predicates with Native AOT safety.
/// </summary>
internal static class FilterExpressionBuilder {
    private static readonly MethodInfo StringContainsMethod =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private static readonly MethodInfo StringStartsWithMethod =
        typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;

    private static readonly MethodInfo StringEndsWithMethod =
        typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;

    /// <summary>
    /// Instance-method <c>string.ToLower()</c>, used instead of <c>ToLowerInvariant()</c> or
    /// <c>StringComparison</c> overloads because it is the one string-casing operation most EF Core
    /// relational providers can translate to SQL (typically <c>LOWER(...)</c>), keeping case-insensitive
    /// comparisons portable between EF Core's InMemory provider and real database providers.
    /// </summary>
    private static readonly MethodInfo ToLowerMethod =
        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

    public static Expression<Func<T, bool>>? BuildFilterPredicate<T>(
        IReadOnlyList<FilterConditionNode> filters,
        QuerySchema<T> schema,
        ParameterExpression parameter) {
        if(filters.Count == 0) return null;

        Expression? combined = null;
        int appliedCount = 0;

        for(int i = 0; i < filters.Count; i++) {
            if(appliedCount >= schema.MaxFilterCount) {
                break;
            }

            FilterConditionNode filter = filters[i];
            if(schema.IsParameterIgnored(filter.Field)) {
                continue;
            }

            if(!schema.TryGetProperty(filter.Field, out QueryProperty<T>? prop) || !schema.IsFilterAllowed(filter.Field, filter.Operator)) {
                continue;
            }

            // Omit empty filters if schema ignores empty filter values, unless property explicitly permits empty strings
            if(string.IsNullOrWhiteSpace(filter.RawValue) && !filter.IsUnary) {
                if(schema.IgnoreEmptyFilterValues && !(prop.AllowEmptyString && prop.PropertyType == typeof(string))) {
                    continue;
                }
            }

            // Defense-in-depth: QuerySchema.Validate() already rejects oversized values with a proper
            // ValidationProblem, but callers invoking ApplyQuery without going through validation first
            // (or a custom pipeline) should still not have an oversized value reach the database.
            if(!string.IsNullOrEmpty(filter.RawValue) && filter.RawValue.Length > schema.MaxFilterValueLength) {
                continue;
            }

            Expression memberExpr = ReplaceParameter(prop.SelectorBody, prop.Parameter, parameter);
            Expression? condition = BuildConditionExpression(
                memberExpr,
                prop,
                filter.Operator,
                filter.RawValue,
                schema.MaxInValuesCount,
                schema.UseCaseInsensitiveTextComparisons);

            if(condition == null) {
                continue;
            }

            combined = combined == null
                ? condition
                : Expression.AndAlso(combined, condition);

            appliedCount++;
        }

        return combined == null ? null : Expression.Lambda<Func<T, bool>>(combined, parameter);
    }

    public static Expression<Func<T, bool>>? BuildSearchPredicate<T>(
        Q q,
        QuerySchema<T> schema,
        ParameterExpression parameter) {
        if(q.IsEmpty || schema.SearchSelectors.Count == 0 || q.Length > schema.MaxSearchTermLength) return null;

        bool caseInsensitive = schema.UseCaseInsensitiveTextComparisons;
        string searchValue = caseInsensitive ? q.Value.ToLower() : q.Value;
        ConstantExpression searchTerm = Expression.Constant(searchValue, typeof(string));
        Expression? combined = null;

        foreach(Expression<Func<T, string?>> selector in schema.SearchSelectors) {
            Expression memberExpr = ReplaceParameter(selector.Body, selector.Parameters[0], parameter);
            Expression notNull = Expression.NotEqual(memberExpr, Expression.Constant(null, typeof(string)));

            // The ToLower() call is only ever evaluated once notNull is known true, because it lives on the
            // right-hand side of Expression.AndAlso below, which short-circuits on compiled delegates just like C#'s &&.
            Expression callTarget = caseInsensitive
                ? Expression.Call(memberExpr, ToLowerMethod)
                : memberExpr;

            Expression contains = Expression.Call(callTarget, StringContainsMethod, searchTerm);
            Expression condition = Expression.AndAlso(notNull, contains);

            combined = combined == null
                ? condition
                : Expression.OrElse(combined, condition);
        }

        return combined == null ? null : Expression.Lambda<Func<T, bool>>(combined, parameter);
    }

    private static Expression? BuildConditionExpression<T>(
        Expression memberExpr,
        QueryProperty<T> prop,
        QueryOperator op,
        string? rawValue,
        int maxInValuesCount,
        bool caseInsensitiveText) {
        Type underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        switch(op) {
            case QueryOperator.IsNull:
                return Expression.Equal(memberExpr, Expression.Constant(null, memberExpr.Type));

            case QueryOperator.IsNotNull:
                return Expression.NotEqual(memberExpr, Expression.Constant(null, memberExpr.Type));

            case QueryOperator.Equal:
            case QueryOperator.NotEqual:
            case QueryOperator.GreaterThan:
            case QueryOperator.GreaterThanOrEqual:
            case QueryOperator.LessThan:
            case QueryOperator.LessThanOrEqual: {
                if(!TryResolveValue(rawValue, prop, underlyingType, out object? converted)) {
                    return null;
                }

                // Case-insensitive equality only applies to Equal/NotEqual on string properties.
                // Ordering operators (gt/gte/lt/lte) keep ordinal semantics regardless of the flag.
                if(caseInsensitiveText &&
                   underlyingType == typeof(string) &&
                   (op == QueryOperator.Equal || op == QueryOperator.NotEqual) &&
                   converted is string equalityValue) {
                    return BuildCaseInsensitiveStringEquality(memberExpr, equalityValue, negate: op == QueryOperator.NotEqual);
                }

                ConstantExpression constant = Expression.Constant(converted, memberExpr.Type);

                return op switch {
                    QueryOperator.Equal => Expression.Equal(memberExpr, constant),
                    QueryOperator.NotEqual => Expression.NotEqual(memberExpr, constant),
                    QueryOperator.GreaterThan => Expression.GreaterThan(memberExpr, constant),
                    QueryOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(memberExpr, constant),
                    QueryOperator.LessThan => Expression.LessThan(memberExpr, constant),
                    QueryOperator.LessThanOrEqual => Expression.LessThanOrEqual(memberExpr, constant),
                    _ => null
                };
            }

            case QueryOperator.Contains:
            case QueryOperator.NotContains:
            case QueryOperator.StartsWith:
            case QueryOperator.NotStartsWith:
            case QueryOperator.EndsWith:
            case QueryOperator.NotEndsWith: {
                if(underlyingType != typeof(string) || rawValue is null) {
                    return null;
                }

                string compareValue = caseInsensitiveText ? rawValue.ToLower() : rawValue;
                ConstantExpression constant = Expression.Constant(compareValue, typeof(string));
                MethodInfo method = op switch {
                    QueryOperator.Contains or QueryOperator.NotContains => StringContainsMethod,
                    QueryOperator.StartsWith or QueryOperator.NotStartsWith => StringStartsWithMethod,
                    QueryOperator.EndsWith or QueryOperator.NotEndsWith => StringEndsWithMethod,
                    _ => StringContainsMethod
                };

                // Same short-circuit-safety reasoning as BuildSearchPredicate: ToLower() on the member is only
                // reached once null-safety has already been established by the surrounding AndAlso/OrElse below.
                Expression callTarget = caseInsensitiveText
                    ? Expression.Call(memberExpr, ToLowerMethod)
                    : memberExpr;

                Expression notNull = Expression.NotEqual(memberExpr, Expression.Constant(null, typeof(string)));
                Expression call = Expression.Call(callTarget, method, constant);

                return op switch {
                    QueryOperator.Contains or QueryOperator.StartsWith or QueryOperator.EndsWith =>
                        Expression.AndAlso(notNull, call),
                    QueryOperator.NotContains or QueryOperator.NotStartsWith or QueryOperator.NotEndsWith =>
                        Expression.OrElse(Expression.Equal(memberExpr, Expression.Constant(null, typeof(string))), Expression.Not(call)),
                    _ => null
                };
            }

            case QueryOperator.In:
            case QueryOperator.NotIn: {
                if(string.IsNullOrEmpty(rawValue)) return null;

                string[] parts = rawValue.Split(QuerySyntax.Comma, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length == 0) return null;

                int count = Math.Min(parts.Length, maxInValuesCount);
                Expression? inCombined = null;

                for(int i = 0; i < count; i++) {
                    if(!TryResolveValue(parts[i], prop, underlyingType, out object? itemVal)) {
                        continue;
                    }

                    Expression equality;
                    if(caseInsensitiveText && underlyingType == typeof(string) && itemVal is string itemStr) {
                        equality = BuildCaseInsensitiveStringEquality(memberExpr, itemStr, negate: op == QueryOperator.NotIn);
                    }
                    else {
                        ConstantExpression constant = Expression.Constant(itemVal, memberExpr.Type);
                        equality = op == QueryOperator.In
                            ? Expression.Equal(memberExpr, constant)
                            : Expression.NotEqual(memberExpr, constant);
                    }

                    inCombined = inCombined == null
                        ? equality
                        : (op == QueryOperator.In ? Expression.OrElse(inCombined, equality) : Expression.AndAlso(inCombined, equality));
                }

                return inCombined;
            }

            case QueryOperator.Between:
            case QueryOperator.NotBetween: {
                if(string.IsNullOrEmpty(rawValue)) return null;

                int delimiterIndex = rawValue.IndexOf(QuerySyntax.RangeDelimiter, StringComparison.Ordinal);
                if(delimiterIndex == -1) return null;

                string lowerStr = rawValue[..delimiterIndex].Trim();
                string upperStr = rawValue[(delimiterIndex + QuerySyntax.RangeDelimiter.Length)..].Trim();

                if(string.IsNullOrEmpty(lowerStr) || string.IsNullOrEmpty(upperStr)) return null;

                if(!TryResolveValue(lowerStr, prop, underlyingType, out object? lowerVal) ||
                   !TryResolveValue(upperStr, prop, underlyingType, out object? upperVal)) {
                    return null;
                }

                ConstantExpression lowerConstant = Expression.Constant(lowerVal, memberExpr.Type);
                ConstantExpression upperConstant = Expression.Constant(upperVal, memberExpr.Type);

                if(op == QueryOperator.Between) {
                    Expression gte = Expression.GreaterThanOrEqual(memberExpr, lowerConstant);
                    Expression lte = Expression.LessThanOrEqual(memberExpr, upperConstant);
                    return Expression.AndAlso(gte, lte);
                }
                else {
                    Expression lt = Expression.LessThan(memberExpr, lowerConstant);
                    Expression gt = Expression.GreaterThan(memberExpr, upperConstant);
                    return Expression.OrElse(lt, gt);
                }
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// Builds a null-safe, case-insensitive string equality (or inequality) expression by lowering both the
    /// member and the constant. The lowering on the member side is guarded against <see langword="null"/>
    /// via short-circuiting <see cref="Expression.AndAlso(Expression, Expression)"/>/<see cref="Expression.OrElse(Expression, Expression)"/>, so it is safe
    /// for nullable string properties even under EF Core's InMemory provider (which executes real delegates).
    /// </summary>
    private static Expression BuildCaseInsensitiveStringEquality(Expression memberExpr, string value, bool negate) {
        Expression memberIsNull = Expression.Equal(memberExpr, Expression.Constant(null, typeof(string)));
        Expression loweredMember = Expression.Call(memberExpr, ToLowerMethod);
        Expression loweredConstant = Expression.Constant(value.ToLower(), typeof(string));
        Expression loweredEqual = Expression.Equal(loweredMember, loweredConstant);

        return negate
            // NotEqual: null member counts as "not equal" to any non-null value; otherwise compare lowered values.
            ? Expression.OrElse(memberIsNull, Expression.Not(loweredEqual))
            // Equal: null member never equals a non-null value; otherwise compare lowered values.
            : Expression.AndAlso(Expression.Not(memberIsNull), loweredEqual);
    }

    private static bool TryResolveValue<T>(
        string? rawValue,
        QueryProperty<T> prop,
        Type underlyingType,
        [NotNullWhen(true)] out object? converted) {
        converted = null;
        if(rawValue is null) {
            return false;
        }

        if(prop.CustomParser != null) {
            try {
                converted = prop.CustomParser(rawValue);
                return converted != null;
            }
            catch {
                converted = null;
                return false;
            }
        }

        return TypeConverterHelper.TryConvertValue(rawValue, underlyingType, out converted);
    }

    private static Expression ReplaceParameter(Expression expression, ParameterExpression source, ParameterExpression target) {
        return new ParameterReplacer(source, target).Visit(expression);
    }

    private sealed class ParameterReplacer(ParameterExpression source, ParameterExpression target) : ExpressionVisitor {
        protected override Expression VisitParameter(ParameterExpression node) {
            return node == source ? target : base.VisitParameter(node);
        }
    }
}