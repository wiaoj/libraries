namespace Wiaoj.Querying.Compilers;

using System.Linq.Expressions;
using Wiaoj.Querying;

/// <summary>
/// Compiles AST filter nodes into strongly-typed LINQ predicate expressions using schema metadata.
/// </summary>
internal static class ExpressionCompiler {
    public static Expression<Func<T, bool>>? CompileFilters<T>(
        IReadOnlyList<FilterConditionNode> conditions,
        QuerySchema<T> schema) {
        if(conditions.Count == 0) {
            return null;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combinedBody = null;

        foreach(var condition in conditions) {
            if(!schema.TryGetFilterDescriptor(condition.Field, out var descriptor)) {
                continue;
            }

            // Replace parameter in the pre-extracted expression body
            var propertyAccess = new ParameterReplacer(descriptor.Parameter, parameter).Visit(descriptor.PropertySelector);
            var conditionExpression = BuildCondition(propertyAccess, condition, descriptor);

            if(conditionExpression != null) {
                combinedBody = combinedBody == null
                    ? conditionExpression
                    : Expression.AndAlso(combinedBody, conditionExpression);
            }
        }

        return combinedBody == null ? null : Expression.Lambda<Func<T, bool>>(combinedBody, parameter);
    }

    public static Expression<Func<T, bool>>? CompileSearch<T>(
        string? searchTerm,
        QuerySchema<T> schema) {
        if(string.IsNullOrWhiteSpace(searchTerm) || schema.SearchSelectors.Count == 0) {
            return null;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var searchConstant = Expression.Constant(searchTerm);
        var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

        Expression? searchBody = null;

        foreach(var selector in schema.SearchSelectors) {
            var replacedBody = new ParameterReplacer(selector.Parameters[0], parameter).Visit(selector.Body);
            var containsCall = Expression.Call(replacedBody, containsMethod, searchConstant);

            searchBody = searchBody == null
                ? containsCall
                : Expression.OrElse(searchBody, containsCall);
        }

        return searchBody == null ? null : Expression.Lambda<Func<T, bool>>(searchBody, parameter);
    }

    private static Expression? BuildCondition<T>(
        Expression propertyAccess,
        FilterConditionNode condition,
        PropertyFilterDescriptor<T> descriptor) {
        var targetType = descriptor.PropertyType;

        // Null checks
        if(condition.Operator is QueryOperator.IsNull || (condition.Operator is QueryOperator.Equal && condition.RawValue == "null")) {
            return Expression.Equal(propertyAccess, Expression.Constant(null, targetType));
        }

        if(condition.Operator is QueryOperator.IsNotNull || (condition.Operator is QueryOperator.NotEqual && condition.RawValue == "null")) {
            return Expression.NotEqual(propertyAccess, Expression.Constant(null, targetType));
        }

        if(condition.RawValue == null) {
            return null;
        }

        var parsedValue = descriptor.ValueParser(condition.RawValue);
        var constant = Expression.Constant(parsedValue, targetType);

        return condition.Operator switch {
            QueryOperator.Equal => Expression.Equal(propertyAccess, constant),
            QueryOperator.NotEqual => Expression.NotEqual(propertyAccess, constant),
            QueryOperator.GreaterThan => Expression.GreaterThan(propertyAccess, constant),
            QueryOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(propertyAccess, constant),
            QueryOperator.LessThan => Expression.LessThan(propertyAccess, constant),
            QueryOperator.LessThanOrEqual => Expression.LessThanOrEqual(propertyAccess, constant),
            QueryOperator.Contains => Expression.Call(propertyAccess, typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!, constant),
            QueryOperator.StartsWith => Expression.Call(propertyAccess, typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!, constant),
            QueryOperator.EndsWith => Expression.Call(propertyAccess, typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!, constant),
            _ => null
        };
    }

    private sealed class ParameterReplacer(ParameterExpression oldParam, ParameterExpression newParam) : ExpressionVisitor {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == oldParam ? newParam : base.VisitParameter(node);
    }
}