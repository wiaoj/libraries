using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;
using Wiaoj.Querying.Expressions;

namespace Wiaoj.Querying;

/// <summary>
/// Configures filtering, searching, sorting rules, security limits, and validation for a target entity with AOT safety.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class QuerySchema<T> {
    internal const uint AllOperatorsMask = uint.MaxValue;

    private readonly Dictionary<string, QueryProperty<T>> _propertiesByExposedName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, QueryProperty<T>> _propertiesByMemberName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Expression<Func<T, string?>>> _searchSelectors = [];
    private readonly List<Expression<Func<T, bool>>> _requiredFilters = [];
    private readonly List<(string MemberPath, Expression<Func<T, bool>> Predicate)> _defaultFilters = [];
    private readonly List<Func<IQueryable<T>, bool, IQueryable<T>>> _defaultSortAppliers = [];
    private readonly HashSet<string> _ignoredParameters = new(StringComparer.OrdinalIgnoreCase);

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
    /// Gets the maximum allowed character length for a single filter's raw value (including each combined value
    /// within IN/NOT IN lists and Between/NotBetween bounds, since those are validated as part of the same raw
    /// string). Defaults to 512. Guards against oversized payloads driving expensive <c>LIKE</c>/<c>IN</c> queries
    /// or excessive parsing work.
    /// </summary>
    public int MaxFilterValueLength { get; private set; } = 512;

    /// <summary>
    /// Gets the maximum allowed character length for the free-text search term (<c>q=</c>). Defaults to 256.
    /// Guards against oversized search terms driving expensive multi-column <c>LIKE</c> scans.
    /// </summary>
    public int MaxSearchTermLength { get; private set; } = 256;

    /// <summary>
    /// Gets a value indicating whether empty or whitespace filter values should be ignored. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IgnoreEmptyFilterValues { get; private set; }

    /// <summary>
    /// Gets a value indicating whether string-based filter and search comparisons (Equal, NotEqual, In, NotIn,
    /// Contains, StartsWith, EndsWith, and free-text search) are performed case-insensitively. Defaults to <see langword="true"/>.
    /// </summary>
    public bool UseCaseInsensitiveTextComparisons { get; private set; } = true;

    /// <summary>
    /// Gets the total number of configured properties.
    /// </summary>
    public int PropertyCount => this._propertiesByMemberName.Count;

    /// <summary>
    /// Gets the list of configured search property selectors.
    /// </summary>
    internal IReadOnlyList<Expression<Func<T, string?>>> SearchSelectors => this._searchSelectors;

    /// <summary>
    /// Gets the list of predicates registered via <see cref="RequireFilter"/> that are always applied,
    /// regardless of request content.
    /// </summary>
    internal IReadOnlyList<Expression<Func<T, bool>>> RequiredFilters => this._requiredFilters;

    /// <summary>
    /// Gets the list of fallback filter rules registered via <see cref="DefaultFilter{TProperty}"/>, each paired
    /// with the member path of the property it is contingent on.
    /// </summary>
    internal IReadOnlyList<(string MemberPath, Expression<Func<T, bool>> Predicate)> DefaultFilterRules => this._defaultFilters;

    /// <summary>
    /// Gets the ordered list of sort appliers registered via <see cref="DefaultSort{TProperty}"/>, applied
    /// only when the incoming request specifies no sort at all.
    /// </summary>
    internal IReadOnlyList<Func<IQueryable<T>, bool, IQueryable<T>>> DefaultSortAppliers => this._defaultSortAppliers;

    /// <summary>
    /// Configures whether empty or whitespace filter values should be ignored during validation and query execution.
    /// </summary>
    /// <param name="ignore"><see langword="true"/> to silently ignore empty filter values; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</param>
    /// <returns>The current schema instance for method chaining.</returns>
    public QuerySchema<T> IgnoreEmptyFilters(bool ignore = true) {
        this.IgnoreEmptyFilterValues = ignore;
        return this;
    }

    /// <summary>
    /// Configures one or more parameter names to be ignored during query validation.
    /// Ignored parameters will not produce validation errors when present in a query request.
    /// </summary>
    /// <param name="parameters">The parameter names to ignore.</param>
    /// <returns>The current schema instance for method chaining.</returns>
    public QuerySchema<T> IgnoreParameters(params ReadOnlySpan<string> parameters) {
        for(int i = 0; i < parameters.Length; i++) {
            string? param = parameters[i];
            if(!string.IsNullOrWhiteSpace(param)) {
                this._ignoredParameters.Add(param.Trim());
            }
        }
        return this;
    }

    /// <summary>
    /// Configures parameter names to be ignored during query validation.
    /// Ignored parameters will not produce validation errors when present in a query request.
    /// </summary>
    /// <param name="parameters">The collection of parameter names to ignore.</param>
    /// <returns>The current schema instance for method chaining.</returns>
    public QuerySchema<T> IgnoreParameters(IEnumerable<string> parameters) {
        ArgumentNullException.ThrowIfNull(parameters);
        foreach(string? param in parameters) {
            if(!string.IsNullOrWhiteSpace(param)) {
                this._ignoredParameters.Add(param.Trim());
            }
        }
        return this;
    }

    /// <summary>
    /// Determines whether the specified parameter name is configured to be ignored by this schema.
    /// </summary>
    /// <param name="parameterName">The parameter name to check.</param>
    /// <returns><see langword="true"/> if the parameter is ignored; otherwise, <see langword="false"/>.</returns>
    public bool IsParameterIgnored(string parameterName) {
        if(string.IsNullOrWhiteSpace(parameterName)) {
            return false;
        }
        return this._ignoredParameters.Contains(parameterName.Trim());
    }

    /// <summary>
    /// Configures whether string-based filter and search comparisons (equality, IN lists, Contains/StartsWith/EndsWith,
    /// and free-text search) are case-insensitive.
    /// </summary>
    /// <param name="enabled">
    /// <see langword="true"/> (default) to compare strings case-insensitively (translated as SQL <c>LOWER(...)</c>
    /// on the target field and value for provider portability); <see langword="false"/> for exact, ordinal,
    /// case-sensitive comparisons.
    /// </param>
    /// <returns>The current schema instance for method chaining.</returns>
    public QuerySchema<T> UseCaseInsensitiveText(bool enabled = true) {
        this.UseCaseInsensitiveTextComparisons = enabled;
        return this;
    }

    /// <summary>
    /// Registers a predicate that is always applied to every query, regardless of what the caller requests
    /// and even when the incoming <see cref="QueryRequest"/> is entirely empty. There is no way for a caller
    /// to bypass or override a required filter through the query string — use this for invariants that must
    /// always hold, such as soft-delete (<c>DeletedAt == null</c>) or publish-state checks.
    /// </summary>
    /// <param name="predicate">The predicate to always apply, expressed against the entity type.</param>
    /// <returns>The current schema instance for method chaining.</returns>
    /// <remarks>
    /// This is not a substitute for row-level security (e.g. multi-tenant isolation). A predicate registered
    /// here only takes effect for callers that route their query through <c>ApplyQuery</c>; it provides no
    /// protection against code elsewhere in the application querying the same <c>DbSet</c> directly. For
    /// invariants that must hold no matter which code path executes, use EF Core's own
    /// <c>ModelBuilder.Entity&lt;T&gt;().HasQueryFilter(...)</c> instead.
    /// </remarks>
    public QuerySchema<T> RequireFilter(Expression<Func<T, bool>> predicate) {
        ArgumentNullException.ThrowIfNull(predicate);
        this._requiredFilters.Add(predicate);
        return this;
    }

    /// <summary>
    /// Registers a fallback predicate tied to <paramref name="propertySelector"/>'s field. The predicate is
    /// applied only when the incoming request contains no filter on that field at all (by exposed name,
    /// case-insensitively); if the caller supplies any filter on that field, this default is skipped entirely
    /// and the caller's own filter takes over — the two are never combined together.
    /// </summary>
    /// <typeparam name="TProperty">The property type; only used to infer the field being defaulted.</typeparam>
    /// <param name="propertySelector">
    /// Selects the field this default is contingent on. The field does not need to be registered as filterable
    /// via <see cref="AllowFilter{TProperty}"/> — a default can exist on a field the caller is never allowed
    /// to override.
    /// </param>
    /// <param name="predicate">The predicate to apply when the field is not present in the request.</param>
    /// <returns>The current schema instance for method chaining.</returns>
    public QuerySchema<T> DefaultFilter<TProperty>(
        Expression<Func<T, TProperty>> propertySelector,
        Expression<Func<T, bool>> predicate) {
        ArgumentNullException.ThrowIfNull(propertySelector);
        ArgumentNullException.ThrowIfNull(predicate);

        string memberPath = ExtractMemberPath(propertySelector.Body);
        this._defaultFilters.Add((memberPath, predicate));
        return this;
    }

    /// <summary>
    /// Registers a fallback sort field applied only when the incoming request specifies no <c>sort</c> at all.
    /// Call multiple times to build a multi-field default order; registration order is preserved as the
    /// primary/secondary/... sort precedence. Ignored entirely the moment the caller supplies any sort.
    /// </summary>
    /// <typeparam name="TProperty">The property type being sorted on.</typeparam>
    /// <param name="selector">The property to sort by.</param>
    /// <param name="direction">The sort direction. Defaults to <see cref="SortDirection.Ascending"/>.</param>
    /// <returns>The current schema instance for method chaining.</returns>
    public QuerySchema<T> DefaultSort<TProperty>(
        Expression<Func<T, TProperty>> selector,
        SortDirection direction = SortDirection.Ascending) {
        ArgumentNullException.ThrowIfNull(selector);

        bool isDescending = direction == SortDirection.Descending;

        this._defaultSortAppliers.Add((query, isFirst) => {
            if(isFirst) {
                return isDescending
                    ? query.OrderByDescending(selector)
                    : query.OrderBy(selector);
            }

            return isDescending
                ? ((IOrderedQueryable<T>)query).ThenByDescending(selector)
                : ((IOrderedQueryable<T>)query).ThenBy(selector);
        });

        return this;
    }

    /// <summary>
    /// Resolves the current exposed (query-string-facing) name for a member path registered via
    /// <see cref="DefaultFilter{TProperty}"/>, honoring any alias applied later via
    /// <see cref="PropertyRuleBuilder{T, TProperty}.HasName"/>. Falls back to the member path itself when the
    /// property was never separately registered via <see cref="Property{TProperty}(Expression{Func{T, TProperty}})"/>.
    /// </summary>
    internal string ResolveExposedName(string memberPath) {
        return this._propertiesByMemberName.TryGetValue(memberPath, out QueryProperty<T>? prop)
            ? prop.ExposedName
            : memberPath;
    }

    /// <summary>
    /// Configures security and abuse limits for query evaluation.
    /// </summary>
    /// <param name="maxFilters">The maximum number of filters allowed per request.</param>
    /// <param name="maxInValues">The maximum number of elements allowed in a single IN/NOT IN list.</param>
    /// <param name="maxSortFields">The maximum number of sort fields allowed per request.</param>
    /// <param name="maxFilterValueLength">
    /// The maximum character length allowed for a single filter's raw value. Defaults to 512.
    /// </param>
    /// <param name="maxSearchTermLength">
    /// The maximum character length allowed for the free-text search term (<c>q=</c>). Defaults to 256.
    /// </param>
    public QuerySchema<T> ConfigureLimits(
        int maxFilters,
        int maxInValues,
        int maxSortFields,
        int maxFilterValueLength = 512,
        int maxSearchTermLength = 256) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFilters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxInValues);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSortFields);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFilterValueLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSearchTermLength);

        this.MaxFilterCount = maxFilters;
        this.MaxInValuesCount = maxInValues;
        this.MaxSortFieldsCount = maxSortFields;
        this.MaxFilterValueLength = maxFilterValueLength;
        this.MaxSearchTermLength = maxSearchTermLength;

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

        // 1. Free-text search term length
        if(!request.Q.IsEmpty && request.Q.Length > this.MaxSearchTermLength) {
            errors ??= [];
            errors.Add(new QueryValidationError(
                propertyName: QuerySyntax.Parameters.Q,
                errorCode: QueryValidationErrorCode.SearchTermTooLong,
                message: $"The search term exceeds the maximum allowed length of {this.MaxSearchTermLength} characters.",
                attemptedValue: Truncate(request.Q.Value)));
        }

        // 2. Security limits: MaxFilterCount
        int activeFilterCount = 0;
        if(this._ignoredParameters.Count == 0) {
            activeFilterCount = request.Filters.Count;
        }
        else {
            for(int i = 0; i < request.Filters.Count; i++) {
                if(!this._ignoredParameters.Contains(request.Filters[i].Field)) {
                    activeFilterCount++;
                }
            }
        }

        if(activeFilterCount > this.MaxFilterCount) {
            errors ??= [];
            errors.Add(new QueryValidationError(
                propertyName: null,
                errorCode: QueryValidationErrorCode.MaxFilterCountExceeded,
                message: $"The request contains {activeFilterCount} filters, which exceeds the maximum limit of {this.MaxFilterCount}."));
        }

        // 3. Security limits: MaxSortFieldsCount
        if(request.Sort.Count > this.MaxSortFieldsCount) {
            errors ??= [];
            errors.Add(new QueryValidationError(
                propertyName: null,
                errorCode: QueryValidationErrorCode.MaxSortFieldsCountExceeded,
                message: $"The request contains {request.Sort.Count} sort fields, which exceeds the maximum limit of {this.MaxSortFieldsCount}."));
        }

        // 4. Validate Sort fields
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

        // 5. Validate Filters
        for(int i = 0; i < request.Filters.Count; i++) {
            FilterConditionNode filter = request.Filters[i];

            if(this._ignoredParameters.Contains(filter.Field)) {
                continue;
            }

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

            // Check if empty filter value should be skipped based on schema policy
            if(string.IsNullOrWhiteSpace(filter.RawValue) && !filter.IsUnary) {
                if(this.IgnoreEmptyFilterValues && !(prop.AllowEmptyString && prop.PropertyType == typeof(string))) {
                    continue;
                }

                if(prop.AllowEmptyString && prop.PropertyType == typeof(string)) {
                    continue;
                }
            }

            // Security limit: MaxFilterValueLength (checked before type/format validation so a single
            // oversized payload always reports as "too long" rather than a confusing format error)
            if(!string.IsNullOrEmpty(filter.RawValue) && filter.RawValue.Length > this.MaxFilterValueLength) {
                errors ??= [];
                errors.Add(new QueryValidationError(
                    propertyName: filter.Field,
                    errorCode: QueryValidationErrorCode.FilterValueTooLong,
                    message: $"The value for field '{filter.Field}' exceeds the maximum allowed length of {this.MaxFilterValueLength} characters.",
                    attemptedValue: Truncate(filter.RawValue)));
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

    /// <summary>
    /// Truncates a value for safe inclusion in a validation error's <c>AttemptedValue</c>, preventing an
    /// oversized payload from being fully echoed back into the response body.
    /// </summary>
    private static string Truncate(string value, int maxLength = 100) {
        return value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "…");
    }

    private static bool TryResolveValidationValue(
        string? rawValue,
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
    public QuerySchema<T> SearchIn(params Expression<Func<T, string?>>[] selectors) {
        ArgumentNullException.ThrowIfNull(selectors);
        for(int i = 0; i < selectors.Length; i++) {
            Expression<Func<T, string?>> selector = selectors[i];
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
/// Internal metadata holding expression bindings, operator bitmask, custom parser, empty string policy, and rules for an entity property.
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
    Func<string, object?>? CustomParser = null,
    bool AllowEmptyString = false);

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
    /// Configures whether empty or whitespace string values are permitted for this property.
    /// </summary>
    /// <param name="allow"><see langword="true"/> to allow empty string values; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</param>
    /// <returns>The property rule builder for method chaining.</returns>
    public PropertyRuleBuilder<T, TProperty> AllowEmpty(bool allow = true) {
        this._property = this._property with { AllowEmptyString = allow };
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