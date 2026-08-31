using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;

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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFilters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInValues);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSortFields);

        this.MaxFilterCount = maxFilters;
        this.MaxInValuesCount = maxInValues;
        this.MaxSortFieldsCount = maxSortFields;

        return this;
    }

    /// <summary>
    /// Configures a specific entity property with fine-grained rules or aliases via a builder.
    /// </summary>
    public PropertyRuleBuilder<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> propertySelector) {
        ArgumentNullException.ThrowIfNull(propertySelector);

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
    public QuerySchema<T> Property<TProperty>(
        Expression<Func<T, TProperty>> propertySelector,
        Action<PropertyRuleBuilder<T, TProperty>> configure) {
        ArgumentNullException.ThrowIfNull(configure);
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
        ArgumentNullException.ThrowIfNull(selectors);
        for(int i = 0; i < selectors.Length; i++) {
            Expression<Func<T, string>> selector = selectors[i];
            ArgumentNullException.ThrowIfNull(selector, nameof(selectors));
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
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        string oldName = this._property.ExposedName;
        this._property = this._property with { ExposedName = alias.Trim() };
        this._schema.UpdateProperty(oldName, this._property);
        return this;
    }

    /// <summary>
    /// Registers a custom parser delegate to convert raw string values into <typeparamref name="TProperty"/>.
    /// </summary>
    public PropertyRuleBuilder<T, TProperty> WithParser(Func<string, TProperty> parser) {
        ArgumentNullException.ThrowIfNull(parser);

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