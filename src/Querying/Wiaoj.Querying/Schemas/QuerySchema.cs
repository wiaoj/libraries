using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;
using Wiaoj.Preconditions;
using Wiaoj.Querying.Expressions;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying;

/// <summary>
/// Configures filtering, searching, sorting rules, and security limits for a target entity with AOT safety.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class QuerySchema<T> {
    internal const uint AllOperatorsMask = uint.MaxValue;

    private readonly Dictionary<string, QueryProperty<T>> _propertiesByExposedName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, QueryProperty<T>> _propertiesByMemberName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Expression<Func<T, string>>> _searchSelectors = [];

    /// <summary>
    /// Gets the maximum allowed number of filters per request. Defaults to 20.
    /// </summary>
    public int MaxFilterCount { get; private set; } = 20;

    /// <summary>
    /// Gets the maximum allowed elements for collection operations (e.g. IN/NOT IN). Defaults to 50.
    /// </summary>
    public int MaxInValuesCount { get; private set; } = 50;

    /// <summary>
    /// Gets the maximum allowed sort fields per request. Defaults to 5.
    /// </summary>
    public int MaxSortFieldsCount { get; private set; } = 5;

    /// <summary>
    /// Gets the total number of configured properties.
    /// </summary>
    public int PropertyCount => this._propertiesByMemberName.Count;

    /// <summary>
    /// Gets the list of configured search property selectors.
    /// </summary>
    internal IReadOnlyList<Expression<Func<T, string>>> SearchSelectors => this._searchSelectors;

    /// <summary>
    /// Configures security and abuse limits for query evaluation.
    /// </summary>
    public QuerySchema<T> ConfigureLimits(int maxFilters, int maxInValues, int maxSortFields) {
        Preca.ThrowIfNegativeOrZero(maxFilters);
        Preca.ThrowIfNegativeOrZero(maxInValues);
        Preca.ThrowIfNegativeOrZero(maxSortFields);

        this.MaxFilterCount = maxFilters;
        this.MaxInValuesCount = maxInValues;
        this.MaxSortFieldsCount = maxSortFields;

        return this;
    }

    /// <summary>
    /// Validates a <see cref="QueryRequest"/> against the configured schema rules, permitted operators, and security limits.
    /// </summary>
    /// <param name="request">The query request to validate.</param>
    /// <returns>A <see cref="QueryValidationResult"/> detailing whether validation succeeded and any diagnostic errors encountered.</returns>
    public QueryValidationResult Validate(QueryRequest request) {
        if(request.IsEmpty) {
            return QueryValidationResult.Success;
        }

        List<QueryValidationError>? errors = null;

        // 1. Security limits: MaxFilterCount
        if(request.Filters.Count > this.MaxFilterCount) {
            errors ??= [];
            errors.Add(new QueryValidationError(
                propertyName: null,
                errorCode: QueryValidationErrorCode.MaxFilterCountExceeded,
                message: $"The request contains {request.Filters.Count} filters, which exceeds the maximum limit of {this.MaxFilterCount}."));
        }

        // 2. Security limits: MaxSortFieldsCount
        if(request.Sort.Count > this.MaxSortFieldsCount) {
            errors ??= [];
            errors.Add(new QueryValidationError(
                propertyName: null,
                errorCode: QueryValidationErrorCode.MaxSortFieldsCountExceeded,
                message: $"The request contains {request.Sort.Count} sort fields, which exceeds the maximum limit of {this.MaxSortFieldsCount}."));
        }

        // 3. Validate Sort fields
        for(int i = 0; i < request.Sort.Count; i++) {
            SortNode sortNode = request.Sort[i];
            if(!IsSortAllowed(sortNode.Field)) {
                errors ??= [];
                errors.Add(new QueryValidationError(
                    propertyName: sortNode.Field,
                    errorCode: QueryValidationErrorCode.FieldNotSortable,
                    message: $"Sorting by field '{sortNode.Field}' is not allowed."));
            }
        }

        // 4. Validate Filters
        for(int i = 0; i < request.Filters.Count; i++) {
            FilterConditionNode filter = request.Filters[i];

            if(!TryGetProperty(filter.Field, out QueryProperty<T>? prop) || !prop.IsFilterable) {
                errors ??= [];
                errors.Add(new QueryValidationError(
                    propertyName: filter.Field,
                    errorCode: QueryValidationErrorCode.FieldNotFilterable,
                    message: $"Filtering by field '{filter.Field}' is not allowed."));
                continue;
            }

            if(!IsFilterAllowed(filter.Field, filter.Operator)) {
                errors ??= [];
                errors.Add(new QueryValidationError(
                    propertyName: filter.Field,
                    errorCode: QueryValidationErrorCode.OperatorNotAllowed,
                    message: $"Operator '{filter.Operator}' is not allowed on field '{filter.Field}'."));
                continue;
            }

            Type underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            switch(filter.Operator) {
                case QueryOperator.IsNull:
                case QueryOperator.IsNotNull:
                    break;

                case QueryOperator.In:
                case QueryOperator.NotIn: {
                    if(!string.IsNullOrEmpty(filter.RawValue)) {
                        string[] parts = filter.RawValue.Split(QuerySyntax.Comma, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                        if(parts.Length > this.MaxInValuesCount) {
                            errors ??= [];
                            errors.Add(new QueryValidationError(
                                propertyName: filter.Field,
                                errorCode: QueryValidationErrorCode.MaxInValuesCountExceeded,
                                message: $"The IN operation on field '{filter.Field}' contains {parts.Length} values, exceeding the maximum limit of {this.MaxInValuesCount}.",
                                attemptedValue: filter.RawValue));
                        }

                        for(int p = 0; p < parts.Length; p++) {
                            if(!TryResolveValidationValue(parts[p], prop, underlyingType, out _)) {
                                errors ??= [];
                                errors.Add(new QueryValidationError(
                                    propertyName: filter.Field,
                                    errorCode: QueryValidationErrorCode.InvalidValueFormat,
                                    message: $"Value '{parts[p]}' is not a valid format for property '{filter.Field}' of type '{underlyingType.Name}'.",
                                    attemptedValue: parts[p]));
                                break;
                            }
                        }
                    }
                    break;
                }

                case QueryOperator.Between:
                case QueryOperator.NotBetween: {
                    if(string.IsNullOrEmpty(filter.RawValue)) {
                        errors ??= [];
                        errors.Add(new QueryValidationError(
                            propertyName: filter.Field,
                            errorCode: QueryValidationErrorCode.MalformedRange,
                            message: $"Range operation on field '{filter.Field}' requires lower and upper bounds separated by '{QuerySyntax.RangeDelimiter}'.",
                            attemptedValue: filter.RawValue));
                        break;
                    }

                    int delimiterIndex = filter.RawValue.IndexOf(QuerySyntax.RangeDelimiter, StringComparison.Ordinal);
                    if(delimiterIndex == -1) {
                        errors ??= [];
                        errors.Add(new QueryValidationError(
                            propertyName: filter.Field,
                            errorCode: QueryValidationErrorCode.MalformedRange,
                            message: $"Range operation on field '{filter.Field}' requires lower and upper bounds separated by '{QuerySyntax.RangeDelimiter}'.",
                            attemptedValue: filter.RawValue));
                        break;
                    }

                    string lowerStr = filter.RawValue[..delimiterIndex].Trim();
                    string upperStr = filter.RawValue[(delimiterIndex + QuerySyntax.RangeDelimiter.Length)..].Trim();

                    if(string.IsNullOrEmpty(lowerStr) || string.IsNullOrEmpty(upperStr)) {
                        errors ??= [];
                        errors.Add(new QueryValidationError(
                            propertyName: filter.Field,
                            errorCode: QueryValidationErrorCode.MalformedRange,
                            message: $"Range operation on field '{filter.Field}' contains empty bounds.",
                            attemptedValue: filter.RawValue));
                        break;
                    }

                    if(!TryResolveValidationValue(lowerStr, prop, underlyingType, out _) ||
                       !TryResolveValidationValue(upperStr, prop, underlyingType, out _)) {
                        errors ??= [];
                        errors.Add(new QueryValidationError(
                            propertyName: filter.Field,
                            errorCode: QueryValidationErrorCode.InvalidValueFormat,
                            message: $"Range boundary values in '{filter.RawValue}' are not valid for property '{filter.Field}' of type '{underlyingType.Name}'.",
                            attemptedValue: filter.RawValue));
                    }
                    break;
                }

                default: {
                    if(filter.RawValue != null && !TryResolveValidationValue(filter.RawValue, prop, underlyingType, out _)) {
                        errors ??= [];
                        errors.Add(new QueryValidationError(
                            propertyName: filter.Field,
                            errorCode: QueryValidationErrorCode.InvalidValueFormat,
                            message: $"Value '{filter.RawValue}' is not a valid format for property '{filter.Field}' of type '{underlyingType.Name}'.",
                            attemptedValue: filter.RawValue));
                    }
                    break;
                }
            }
        }

        return errors == null ? QueryValidationResult.Success : new QueryValidationResult(errors);
    }

    private static bool TryResolveValidationValue(string? rawValue,
                                                  QueryProperty<T> prop,
                                                  Type underlyingType,
                                                  out object? result) {
        result = null;
        if(rawValue is null) return false;

        if(prop.CustomParser != null) {
            try {
                result = prop.CustomParser(rawValue);
                return result != null;
            }
            catch {
                result = null;
                return false;
            }
        }

        return TypeConverterHelper.TryConvertValue(rawValue, underlyingType, out result);
    }

    /// <summary>
    /// Configures a specific entity property with fine-grained rules or aliases via a builder.
    /// </summary>
    public PropertyRuleBuilder<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> propertySelector) {
        Preca.ThrowIfNull(propertySelector);

        string memberPath = ExtractMemberPath(propertySelector.Body);

        if(!this._propertiesByMemberName.TryGetValue(memberPath, out QueryProperty<T>? rule)) {
            rule = new QueryProperty<T>(
                MemberName: memberPath,
                ExposedName: memberPath,
                PropertyType: typeof(TProperty),
                SelectorBody: propertySelector.Body,
                Parameter: propertySelector.Parameters[0]);

            this._propertiesByMemberName[memberPath] = rule;
            this._propertiesByExposedName[memberPath] = rule;
        }

        return new PropertyRuleBuilder<T, TProperty>(this, rule);
    }

    /// <summary>
    /// Configures a specific entity property using an inline action and returns the schema for method chaining.
    /// </summary>
    public QuerySchema<T> Property<TProperty>(Expression<Func<T, TProperty>> propertySelector,
                                              Action<PropertyRuleBuilder<T, TProperty>> configure) {
        Preca.ThrowIfNull(configure);
        PropertyRuleBuilder<T, TProperty> builder = Property(propertySelector);
        configure(builder);
        return this;
    }

    /// <summary>
    /// Registers a property as filterable using all or specific operators.
    /// </summary>
    public QuerySchema<T> AllowFilter<TProperty>(
        Expression<Func<T, TProperty>> propertySelector,
        params QueryOperator[] operators) {
        Property(propertySelector).AllowFilter(operators);
        return this;
    }

    /// <summary>
    /// Registers multiple properties as filterable in bulk using default operators.
    /// </summary>
    public QuerySchema<T> AllowFilter<T1, T2>(Expression<Func<T, T1>> p1, Expression<Func<T, T2>> p2) {
        AllowFilter(p1);
        AllowFilter(p2);
        return this;
    }

    /// <summary>
    /// Registers multiple properties as filterable in bulk using default operators.
    /// </summary>
    public QuerySchema<T> AllowFilter<T1, T2, T3>(
        Expression<Func<T, T1>> p1,
        Expression<Func<T, T2>> p2,
        Expression<Func<T, T3>> p3) {
        AllowFilter(p1);
        AllowFilter(p2);
        AllowFilter(p3);
        return this;
    }

    /// <summary>
    /// Registers a property as sortable.
    /// </summary>
    public QuerySchema<T> AllowSort<TProperty>(Expression<Func<T, TProperty>> propertySelector) {
        Property(propertySelector).AllowSort();
        return this;
    }

    /// <summary>
    /// Registers multiple properties as sortable in bulk.
    /// </summary>
    public QuerySchema<T> AllowSort<T1, T2>(Expression<Func<T, T1>> p1, Expression<Func<T, T2>> p2) {
        AllowSort(p1);
        AllowSort(p2);
        return this;
    }

    /// <summary>
    /// Registers multiple properties as sortable in bulk.
    /// </summary>
    public QuerySchema<T> AllowSort<T1, T2, T3>(
        Expression<Func<T, T1>> p1,
        Expression<Func<T, T2>> p2,
        Expression<Func<T, T3>> p3) {
        AllowSort(p1);
        AllowSort(p2);
        AllowSort(p3);
        return this;
    }

    /// <summary>
    /// Configures properties to be queried during free-text search (<c>q=term</c>).
    /// </summary>
    public QuerySchema<T> SearchIn(params Expression<Func<T, string>>[] selectors) {
        Preca.ThrowIfNull(selectors);
        for(int i = 0; i < selectors.Length; i++) {
            Expression<Func<T, string>> selector = selectors[i];
            Preca.ThrowIfNull(selector, nameof(selectors));
            this._searchSelectors.Add(selector);
        }
        return this;
    }

    /// <summary>
    /// Determines whether the specified field name is allowed for filtering with any operator.
    /// </summary>
    public bool IsFilterAllowed(string fieldName) {
        if(string.IsNullOrWhiteSpace(fieldName)) return false;
        return this._propertiesByExposedName.TryGetValue(fieldName, out QueryProperty<T>? prop) && prop.IsFilterable;
    }

    /// <summary>
    /// Determines whether the specified field name is allowed for filtering with a specific operator.
    /// </summary>
    public bool IsFilterAllowed(string fieldName, QueryOperator queryOperator) {
        if(string.IsNullOrWhiteSpace(fieldName)) return false;
        if(!this._propertiesByExposedName.TryGetValue(fieldName, out QueryProperty<T>? prop) || !prop.IsFilterable) {
            return false;
        }

        return (prop.AllowedOperatorsMask & (1u << (byte)queryOperator)) != 0;
    }

    /// <summary>
    /// Determines whether the specified field name is allowed for sorting.
    /// </summary>
    public bool IsSortAllowed(string fieldName) {
        if(string.IsNullOrWhiteSpace(fieldName)) return false;
        return this._propertiesByExposedName.TryGetValue(fieldName, out QueryProperty<T>? prop) && prop.IsSortable;
    }

    /// <summary>
    /// Attempts to retrieve property metadata for a given exposed field name.
    /// </summary>
    internal bool TryGetProperty(string fieldName, [NotNullWhen(true)] out QueryProperty<T>? property) {
        if(string.IsNullOrWhiteSpace(fieldName)) {
            property = null;
            return false;
        }
        return this._propertiesByExposedName.TryGetValue(fieldName, out property);
    }

    internal void UpdateProperty(string previousExposedName, QueryProperty<T> updated) {
        if(this._propertiesByExposedName.TryGetValue(updated.ExposedName, out QueryProperty<T>? existing) &&
           !string.Equals(existing.MemberName, updated.MemberName, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException($"Alias '{updated.ExposedName}' is already registered for property '{existing.MemberName}'.");
        }

        this._propertiesByExposedName.Remove(previousExposedName);
        this._propertiesByExposedName[updated.ExposedName] = updated;
        this._propertiesByMemberName[updated.MemberName] = updated;
    }

    internal static uint CreateOperatorMask(ReadOnlySpan<QueryOperator> operators) {
        if(operators.IsEmpty) return AllOperatorsMask;

        uint mask = 0;
        for(int i = 0; i < operators.Length; i++) {
            mask |= (1u << (byte)operators[i]);
        }
        return mask;
    }

    private static string ExtractMemberPath(Expression expression) {
        while(expression is UnaryExpression unary) {
            expression = unary.Operand;
        }

        if(expression is not MemberExpression memberExpr) {
            throw new ArgumentException("Expression must point directly to a member property or nested navigation path.", nameof(expression));
        }

        Stack<string> stack = new();
        Expression? current = memberExpr;

        while(current is MemberExpression m) {
            stack.Push(m.Member.Name);
            current = m.Expression;
            while(current is UnaryExpression u) {
                current = u.Operand;
            }
        }

        if(current is not ParameterExpression) {
            throw new ArgumentException("Expression must originate from the root entity parameter.", nameof(expression));
        }

        StringBuilder sb = new();
        while(stack.Count > 0) {
            sb.Append(stack.Pop());
            if(stack.Count > 0) sb.Append('.');
        }

        return sb.ToString();
    }
}

/// <summary>
/// Internal metadata holding expression bindings, operator bitmask, custom parser, and rules for an entity property.
/// </summary>
internal sealed record QueryProperty<T>(
    string MemberName,
    string ExposedName,
    Type PropertyType,
    Expression SelectorBody,
    ParameterExpression Parameter,
    bool IsFilterable = false,
    bool IsSortable = false,
    uint AllowedOperatorsMask = 0,
    Func<IQueryable<T>, bool, bool, IQueryable<T>>? SortApplier = null,
    Func<string, object?>? CustomParser = null);

/// <summary>
/// Fluent builder for configuring fine-grained rules on a specific property.
/// </summary>
public sealed class PropertyRuleBuilder<T, TProperty> {
    private readonly QuerySchema<T> _schema;
    private QueryProperty<T> _property;

    internal PropertyRuleBuilder(QuerySchema<T> schema, QueryProperty<T> property) {
        this._schema = schema;
        this._property = property;
    }

    /// <summary>
    /// Sets a custom exposed name (alias) for the property in query parameters.
    /// </summary>
    public PropertyRuleBuilder<T, TProperty> HasName(string alias) {
        Preca.ThrowIfNullOrWhiteSpace(alias);
        string oldName = this._property.ExposedName;
        this._property = this._property with { ExposedName = alias.Trim() };
        this._schema.UpdateProperty(oldName, this._property);
        return this;
    }

    /// <summary>
    /// Registers a custom parser delegate to convert raw string values into <typeparamref name="TProperty"/>.
    /// </summary>
    public PropertyRuleBuilder<T, TProperty> WithParser(Func<string, TProperty> parser) {
        Preca.ThrowIfNull(parser);

        this._property = this._property with {
            CustomParser = raw => parser(raw)
        };

        this._schema.UpdateProperty(this._property.ExposedName, this._property);
        return this;
    }

    /// <summary>
    /// Marks the property as allowed for filtering with all or specific operators.
    /// </summary>
    public PropertyRuleBuilder<T, TProperty> AllowFilter(params QueryOperator[] operators) {
        uint newMask = QuerySchema<T>.CreateOperatorMask(operators);
        uint combinedMask = this._property.AllowedOperatorsMask | newMask;

        this._property = this._property with {
            IsFilterable = true,
            AllowedOperatorsMask = combinedMask
        };

        this._schema.UpdateProperty(this._property.ExposedName, this._property);
        return this;
    }

    /// <summary>
    /// Marks the property as allowed for sorting.
    /// </summary>
    public PropertyRuleBuilder<T, TProperty> AllowSort() {
        Expression<Func<T, TProperty>> lambda = Expression.Lambda<Func<T, TProperty>>(
            this._property.SelectorBody,
            this._property.Parameter);

        this._property = this._property with {
            IsSortable = true,
            SortApplier = (query, isDescending, isFirst) => {
                if(isFirst) {
                    return isDescending
                        ? query.OrderByDescending(lambda)
                        : query.OrderBy(lambda);
                }

                return isDescending
                    ? ((IOrderedQueryable<T>)query).ThenByDescending(lambda)
                    : ((IOrderedQueryable<T>)query).ThenBy(lambda);
            }
        };

        this._schema.UpdateProperty(this._property.ExposedName, this._property);
        return this;
    }
}