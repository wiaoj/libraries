using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Wiaoj.Querying.Parsers;

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
            if(!schema.TryGetProperty(filter.Field, out QueryProperty<T>? prop) || !schema.IsFilterAllowed(filter.Field, filter.Operator)) {
                continue;
            }

            Expression memberExpr = ReplaceParameter(prop.SelectorBody, prop.Parameter, parameter);
            Expression? condition = BuildConditionExpression(memberExpr, prop, filter.Operator, filter.RawValue, schema.MaxInValuesCount);

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
        if(q.IsEmpty || schema.SearchSelectors.Count == 0) return null;

        ConstantExpression searchTerm = Expression.Constant(q.Value, typeof(string));
        Expression? combined = null;

        foreach(Expression<Func<T, string>> selector in schema.SearchSelectors) {
            Expression memberExpr = ReplaceParameter(selector.Body, selector.Parameters[0], parameter);
            Expression notNull = Expression.NotEqual(memberExpr, Expression.Constant(null, typeof(string)));
            Expression contains = Expression.Call(memberExpr, StringContainsMethod, searchTerm);
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
        int maxInValuesCount) {
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
                if(!TryResolveValue(rawValue, prop, underlyingType, out var converted)) {
                    return null;
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

                ConstantExpression constant = Expression.Constant(rawValue, typeof(string));
                MethodInfo method = op switch {
                    QueryOperator.Contains or QueryOperator.NotContains => StringContainsMethod,
                    QueryOperator.StartsWith or QueryOperator.NotStartsWith => StringStartsWithMethod,
                    QueryOperator.EndsWith or QueryOperator.NotEndsWith => StringEndsWithMethod,
                    _ => StringContainsMethod
                };

                Expression notNull = Expression.NotEqual(memberExpr, Expression.Constant(null, typeof(string)));
                Expression call = Expression.Call(memberExpr, method, constant);

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
                    if(!TryResolveValue(parts[i], prop, underlyingType, out var itemVal)) {
                        continue;
                    }

                    ConstantExpression constant = Expression.Constant(itemVal, memberExpr.Type);
                    Expression equality = op == QueryOperator.In
                        ? Expression.Equal(memberExpr, constant)
                        : Expression.NotEqual(memberExpr, constant);

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

                if(!TryResolveValue(lowerStr, prop, underlyingType, out var lowerVal) ||
                   !TryResolveValue(upperStr, prop, underlyingType, out var upperVal)) {
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