using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Wiaoj.Querying.Expressions;

namespace Wiaoj.Querying;

/// <summary>
/// Configures filtering, searching, sorting rules, security limits, and validation for a target entity with AOT safety.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class QuerySchema<T> : IQuerySchemaParameters, DependencyInjection.IQuerySchemaNaming {
    internal const uint AllOperatorsMask = uint.MaxValue;

    private readonly Dictionary<string, QueryProperty<T>> _propertiesByExposedName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, QueryProperty<T>> _propertiesByMemberName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Expression<Func<T, string?>>> _searchSelectors = [];
    private readonly List<Expression<Func<T, bool>>> _requiredFilters = [];
    private readonly List<(string MemberPath, Expression<Func<T, bool>> Predicate)> _defaultFilters = [];
    private readonly List<Func<IQueryable<T>, bool, IQueryable<T>>> _defaultSortAppliers = [];
    private readonly HashSet<string> _ignoredParameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allowedParameters = new(StringComparer.OrdinalIgnoreCase);
    private bool _ignoreGlobalParameters;
    private readonly Dictionary<string, string> _aliasesToMember = new(StringComparer.OrdinalIgnoreCase);
    private JsonNamingPolicy? _fieldNamingPolicy;

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
    /// Describes every field this schema exposes, under the name callers write in a query.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The schema already knows exactly which fields are filterable, which are sortable, and which operators
    /// each one permits — it has to, in order to reject anything else. Until now that knowledge could only be
    /// used to say no. Exposing it lets the same rules be published: an OpenAPI document that lists the real
    /// fields and operators, a client that discovers them, a test that asserts them.
    /// </para>
    /// <para>
    /// Read-only by construction. Descriptors carry no expressions, parsers or appliers, so nothing here can
    /// be used to reach around the schema's own validation.
    /// </para>
    /// </remarks>
    public IReadOnlyList<QueryFieldDescriptor> DescribeFields() {
        List<QueryFieldDescriptor> descriptors = new(this._propertiesByMemberName.Count);

        foreach(QueryProperty<T> property in this._propertiesByMemberName.Values) {
            descriptors.Add(new QueryFieldDescriptor(
                PublicName(property),
                property.PropertyType,
                property.IsFilterable,
                property.IsSortable,
                DescribeOperators(property)) {
                Description = property.Description,
                IsCustom = property.IsCustom
            });
        }

        descriptors.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        return descriptors;
    }

    /// <summary>
    /// Expands a property's operator bitmask into the operators it permits.
    /// </summary>
    private static IReadOnlyList<QueryOperator> DescribeOperators(QueryProperty<T> property) {
        if(!property.IsFilterable || property.AllowedOperatorsMask == 0) {
            return [];
        }

        List<QueryOperator> operators = [];

        foreach(QueryOperator candidate in Enum.GetValues<QueryOperator>()) {
            if((property.AllowedOperatorsMask & (1u << (byte)candidate)) != 0) {
                operators.Add(candidate);
            }
        }

        return operators;
    }

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
    /// Gets a value indicating whether this schema bypasses all globally configured ignored parameters from <see cref="QueryOptions"/>.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool IgnoresGlobalParameters => this._ignoreGlobalParameters;

    /// <summary>
    /// Configures this schema to bypass all globally configured ignored parameters from <see cref="QueryOptions"/>.
    /// </summary>
    /// <returns>The current schema instance for method chaining.</returns>
    public QuerySchema<T> IgnoreGlobalParameters() => IgnoreGlobalParameters(true);

    /// <summary>
    /// Configures whether this schema bypasses all globally configured ignored parameters from <see cref="QueryOptions"/>.
    /// When set to <see langword="true"/>, globally ignored parameters will not be ignored for this schema.
    /// </summary>
    /// <param name="ignore"><see langword="true"/> to ignore global parameters; otherwise, <see langword="false"/>.</param>
    /// <returns>The current schema instance for method chaining.</returns>
    public QuerySchema<T> IgnoreGlobalParameters(bool ignore) {
        this._ignoreGlobalParameters = ignore;
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
                string trimmed = param.Trim();
                this._ignoredParameters.Add(trimmed);
                this._allowedParameters.Remove(trimmed);
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
                string trimmed = param.Trim();
                this._ignoredParameters.Add(trimmed);
                this._allowedParameters.Remove(trimmed);
            }
        }
        return this;
    }

    /// <summary>
    /// Configures one or more parameter names to be explicitly allowed (un-ignored) for this schema.
    /// This overrides any global or inherited parameter ignoring rules.
    /// </summary>
    /// <param name="parameters">The parameter names to allow.</param>
    /// <returns>The current schema instance for method chaining.</returns>
    public QuerySchema<T> AllowParameter(params ReadOnlySpan<string> parameters) => AllowParameters(parameters);

    /// <summary>
    /// Configures one or more parameter names to be explicitly allowed (un-ignored) for this schema.
    /// This overrides any global or inherited parameter ignoring rules.
    /// </summary>
    /// <param name="parameters">The parameter names to allow.</param>
    /// <returns>The current schema instance for method chaining.</returns>
    public QuerySchema<T> AllowParameters(params ReadOnlySpan<string> parameters) {
        for(int i = 0; i < parameters.Length; i++) {
            string? param = parameters[i];
            if(!string.IsNullOrWhiteSpace(param)) {
                string trimmed = param.Trim();
                this._allowedParameters.Add(trimmed);
                this._ignoredParameters.Remove(trimmed);
            }
        }
        return this;
    }

    /// <summary>
    /// Configures parameter names to be explicitly allowed (un-ignored) for this schema.
    /// This overrides any global or inherited parameter ignoring rules.
    /// </summary>
    /// <param name="parameters">The collection of parameter names to allow.</param>
    /// <returns>The current schema instance for method chaining.</returns>
    public QuerySchema<T> AllowParameters(IEnumerable<string> parameters) {
        ArgumentNullException.ThrowIfNull(parameters);
        foreach(string? param in parameters) {
            if(!string.IsNullOrWhiteSpace(param)) {
                string trimmed = param.Trim();
                this._allowedParameters.Add(trimmed);
                this._ignoredParameters.Remove(trimmed);
            }
        }
        return this;
    }

    /// <summary>
    /// Determines whether the specified parameter name is explicitly allowed (un-ignored) by this schema.
    /// </summary>
    /// <param name="parameterName">The parameter name to check.</param>
    /// <returns><see langword="true"/> if the parameter is explicitly allowed; otherwise, <see langword="false"/>.</returns>
    public bool IsParameterAllowed(string parameterName) {
        if(string.IsNullOrWhiteSpace(parameterName)) {
            return false;
        }
        return this._allowedParameters.Contains(parameterName.Trim());
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

        string trimmed = parameterName.Trim();
        if(this._allowedParameters.Contains(trimmed)) {
            return false;
        }

        return this._ignoredParameters.Contains(trimmed);
    }

    /// <summary>
    /// Determines whether the specified parameter name is ignored, taking into account schema-level rules
    /// and optional global options.
    /// </summary>
    /// <param name="parameterName">The parameter name to check.</param>
    /// <param name="globalOptions">Optional global query options.</param>
    /// <returns><see langword="true"/> if the parameter is ignored; otherwise, <see langword="false"/>.</returns>
    public bool IsParameterIgnored(string parameterName, QueryOptions? globalOptions) {
        if(string.IsNullOrWhiteSpace(parameterName)) {
            return false;
        }

        string trimmed = parameterName.Trim();
        if(this._allowedParameters.Contains(trimmed)) {
            return false;
        }

        if(this._ignoredParameters.Contains(trimmed)) {
            return true;
        }

        if(!this._ignoreGlobalParameters && globalOptions?.IgnoredParameters.Contains(trimmed) == true) {
            return true;
        }

        return false;
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
    public QueryValidationResult Validate(QueryRequest request) => Validate(request, null);

    /// <summary>
    /// Validates a <see cref="QueryRequest"/> against the configured schema rules, permitted operators, security limits,
    /// and optional global options.
    /// </summary>
    /// <param name="request">The query request to validate.</param>
    /// <param name="options">Optional global query options.</param>
    /// <returns>A <see cref="QueryValidationResult"/> detailing whether validation succeeded and any diagnostic errors encountered.</returns>
    public QueryValidationResult Validate(QueryRequest request, QueryOptions? options) {
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
        for(int i = 0; i < request.Filters.Count; i++) {
            if(!IsParameterIgnored(request.Filters[i].Field, options)) {
                activeFilterCount++;
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

            if(IsParameterIgnored(filter.Field, options)) {
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

        if(this._propertiesByMemberName.TryGetValue(memberPath, out QueryProperty<T>? existing) && existing.IsCustom) {
            throw new InvalidOperationException(
                $"'{memberPath}' is already declared as a custom filter; a property cannot share its name.");
        }

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
    /// Declares a filter that is not an entity member — computed through a subquery, answered by another
    /// service, or otherwise applied by the endpoint itself.
    /// </summary>
    /// <typeparam name="TValue">The type its value parses to.</typeparam>
    /// <param name="name">The name callers write, used exactly as given.</param>
    /// <returns>A builder to allow operators, set a parser and describe the filter.</returns>
    /// <remarks>
    /// <para>
    /// Filters like <c>hasScreenshot</c> or <c>statuses</c> used to live beside the schema — bound with
    /// <c>[AsParameters]</c> and hidden from validation with <c>IgnoreParameters</c> — so they were neither
    /// validated nor described, and a generated client could not send them. Declared here, they are validated
    /// like any field (operator, value type, limits), described in the document, kept on this side by
    /// <c>Partition</c>, and read back typed with <see cref="TryGetFilterValue{TValue}"/> and
    /// <see cref="GetFilterValues{TValue}"/>.
    /// </para>
    /// <para>
    /// <c>ApplyQuery</c> does not apply a filter declared this way; the endpoint does, with the value it reads.
    /// To have the engine apply it, use the overload taking a predicate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// CustomFilter&lt;bool&gt;("hasScreenshot").AllowFilter(QueryOperator.Equal);
    /// CustomFilter&lt;EntryStatus&gt;("statuses").AllowFilter(QueryOperator.In);
    ///
    /// // in the handler
    /// if(schema.TryGetFilterValue&lt;bool&gt;(query.Value, "hasScreenshot", out bool hasScreenshot)) { ... }
    /// IReadOnlyList&lt;EntryStatus&gt; statuses = schema.GetFilterValues&lt;EntryStatus&gt;(query.Value, "statuses");
    /// </code>
    /// </example>
    public PropertyRuleBuilder<T, TValue> CustomFilter<TValue>(string name) {
        return DeclareCustomFilter<TValue>(name, predicate: null);
    }

    /// <summary>
    /// Declares a filter that is not an entity member, applied by the query engine through
    /// <paramref name="predicate"/>.
    /// </summary>
    /// <typeparam name="TValue">The type its value parses to.</typeparam>
    /// <param name="name">The name callers write, used exactly as given.</param>
    /// <param name="predicate">Given an entity and the parsed value, whether the entity matches.</param>
    /// <returns>A builder to allow operators, set a parser and describe the filter.</returns>
    /// <remarks>
    /// Supports <see cref="QueryOperator.Equal"/>, <see cref="QueryOperator.NotEqual"/>,
    /// <see cref="QueryOperator.In"/> and <see cref="QueryOperator.NotIn"/>: the predicate is evaluated per
    /// value and combined. The predicate must be translatable by the query provider.
    /// </remarks>
    /// <example>
    /// <code>
    /// CustomFilter&lt;bool&gt;("hasScreenshot", (key, has) => key.Screenshots.Any() == has);
    /// </code>
    /// </example>
    public PropertyRuleBuilder<T, TValue> CustomFilter<TValue>(string name, Expression<Func<T, TValue, bool>> predicate) {
        ArgumentNullException.ThrowIfNull(predicate);
        return DeclareCustomFilter<TValue>(name, predicate);
    }

    private PropertyRuleBuilder<T, TValue> DeclareCustomFilter<TValue>(string name, LambdaExpression? predicate) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string trimmed = name.Trim();

        if(TryGetProperty(trimmed, out QueryProperty<T>? existing)) {
            throw new InvalidOperationException(
                $"'{trimmed}' already names '{existing.MemberName}' on this schema; a custom filter needs its own name.");
        }

        ParameterExpression entity = Expression.Parameter(typeof(T), "x");

        QueryProperty<T> rule = new(
            MemberName: trimmed,
            ExposedName: trimmed,
            PropertyType: typeof(TValue),
            SelectorBody: Expression.Default(typeof(TValue)),
            Parameter: entity,
            IsExplicitlyNamed: true,
            IsCustom: true,
            CustomPredicate: predicate);

        this._propertiesByMemberName[trimmed] = rule;
        this._propertiesByExposedName[trimmed] = rule;

        return new PropertyRuleBuilder<T, TValue>(this, rule);
    }

    /// <summary>
    /// Reads the value of an equality filter from <paramref name="request"/>, parsed as the schema declares it.
    /// </summary>
    /// <typeparam name="TValue">The declared value type.</typeparam>
    /// <param name="request">The request carrying the filter.</param>
    /// <param name="name">The filter name, or any alias the schema accepts for it.</param>
    /// <param name="value">The parsed value, when present.</param>
    /// <returns><see langword="true"/> when the request carries a parseable equality filter on the field.</returns>
    /// <exception cref="InvalidOperationException">The schema declares no such field, or declares it with a different type.</exception>
    public bool TryGetFilterValue<TValue>(QueryRequest request, string name, [MaybeNullWhen(false)] out TValue value) {
        QueryProperty<T> property = RequireReadable<TValue>(name);
        Type underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        foreach(FilterConditionNode filter in request.Filters) {
            if(filter.Operator == QueryOperator.Equal && Refers(filter.Field, property) &&
               TryResolveValidationValue(filter.RawValue, property, underlying, out object? parsed) && parsed is TValue typed) {
                value = typed;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Reads every value the request filters a field on — from equality and <c>in</c> filters — parsed as the
    /// schema declares it.
    /// </summary>
    /// <typeparam name="TValue">The declared value type.</typeparam>
    /// <param name="request">The request carrying the filters.</param>
    /// <param name="name">The filter name, or any alias the schema accepts for it.</param>
    /// <returns>The parsed values, in order; empty when the request does not filter the field.</returns>
    /// <exception cref="InvalidOperationException">The schema declares no such field, or declares it with a different type.</exception>
    public IReadOnlyList<TValue> GetFilterValues<TValue>(QueryRequest request, string name) {
        QueryProperty<T> property = RequireReadable<TValue>(name);
        Type underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        List<TValue> values = [];

        foreach(FilterConditionNode filter in request.Filters) {
            if(!Refers(filter.Field, property) || string.IsNullOrEmpty(filter.RawValue)) {
                continue;
            }

            IEnumerable<string> raw = filter.Operator switch {
                QueryOperator.Equal => [filter.RawValue],
                QueryOperator.In => filter.RawValue.Split(QuerySyntax.Comma, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                _ => []
            };

            foreach(string item in raw) {
                if(TryResolveValidationValue(item, property, underlying, out object? parsed) && parsed is TValue typed) {
                    values.Add(typed);
                }
            }
        }

        return values;
    }

    private QueryProperty<T> RequireReadable<TValue>(string name) {
        if(!TryGetProperty(name, out QueryProperty<T>? property)) {
            throw new InvalidOperationException($"The schema declares no field named '{name}'.");
        }

        Type declared = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        Type requested = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);

        if(declared != requested) {
            throw new InvalidOperationException(
                $"'{name}' is declared as {property.PropertyType.Name}, not {typeof(TValue).Name}.");
        }

        return property;
    }

    private bool Refers(string field, QueryProperty<T> property) {
        return TryGetProperty(field, out QueryProperty<T>? resolved)
            && string.Equals(resolved.MemberName, property.MemberName, StringComparison.OrdinalIgnoreCase);
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
        return TryGetProperty(fieldName, out QueryProperty<T>? prop) && prop.IsFilterable;
    }

    /// <summary>
    /// Determines whether the specified field name is allowed for filtering with a specific operator.
    /// </summary>
    public bool IsFilterAllowed(string fieldName, QueryOperator queryOperator) {
        if(string.IsNullOrWhiteSpace(fieldName)) return false;
        if(!TryGetProperty(fieldName, out QueryProperty<T>? prop) || !prop.IsFilterable) {
            return false;
        }

        return (prop.AllowedOperatorsMask & (1u << (byte)queryOperator)) != 0;
    }

    /// <summary>
    /// Determines whether the specified field name is allowed for sorting.
    /// </summary>
    public bool IsSortAllowed(string fieldName) {
        if(string.IsNullOrWhiteSpace(fieldName)) return false;
        return TryGetProperty(fieldName, out QueryProperty<T>? prop) && prop.IsSortable;
    }

    /// <summary>
    /// Attempts to retrieve property metadata for a given exposed field name.
    /// </summary>
    internal bool TryGetProperty(string fieldName, [NotNullWhen(true)] out QueryProperty<T>? property) {
        if(string.IsNullOrWhiteSpace(fieldName)) {
            property = null;
            return false;
        }

        if(this._propertiesByExposedName.TryGetValue(fieldName, out property)) {
            return true;
        }

        // A naming-policy alias maps to the member, not to a property record: records are replaced on every
        // builder call, so an alias holding one would go stale the moment a rule was added after it.
        return this._aliasesToMember.TryGetValue(fieldName, out string? member)
            && this._propertiesByMemberName.TryGetValue(member, out property);
    }

    internal void UpdateProperty(string previousExposedName, QueryProperty<T> updated) {
        if(this._propertiesByExposedName.TryGetValue(updated.ExposedName, out QueryProperty<T>? existing) &&
           !string.Equals(existing.MemberName, updated.MemberName, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException($"Alias '{updated.ExposedName}' is already registered for property '{existing.MemberName}'.");
        }

        this._propertiesByExposedName.Remove(previousExposedName);
        this._propertiesByExposedName[updated.ExposedName] = updated;
        this._propertiesByMemberName[updated.MemberName] = updated;

        if(this._fieldNamingPolicy is not null) {
            RebuildNamingAliases();
        }
    }

    /// <summary>
    /// Gets the naming policy applied to field names that were not named explicitly, if any.
    /// </summary>
    public JsonNamingPolicy? FieldNamingPolicy => this._fieldNamingPolicy;

    /// <summary>
    /// Accepts, and publishes, field names rendered through <paramref name="policy"/>.
    /// </summary>
    /// <param name="policy">The policy to apply; <see langword="null"/> removes a previously applied one.</param>
    /// <returns>The schema, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// A field's exposed name defaults to its CLR member path — <c>ContentType</c> — while the application's
    /// bodies are usually written through a naming policy — <c>contentType</c>. The response then says one thing
    /// and the filter parameter another. This makes the schema accept the policy's rendering and report it from
    /// <see cref="DescribeFields"/>, so a generated document names fields the way the rest of the API does.
    /// </para>
    /// <para>
    /// The rendering is added as an alias; the original name keeps working, so applying a policy breaks no
    /// existing caller. It is also what makes a non-case-only policy safe: <c>content_type</c> would not match
    /// <c>ContentType</c> case-insensitively, and a document advertising it without the alias would describe a
    /// parameter the server rejects.
    /// </para>
    /// <para>
    /// Fields named with <see cref="PropertyRuleBuilder{T, TProperty}.HasName"/> are left exactly as written.
    /// Nested member paths are rendered segment by segment.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The policy renders two fields to the same name, or a field to another field's existing name.
    /// </exception>
    public QuerySchema<T> UseFieldNamingPolicy(JsonNamingPolicy? policy) {
        this._fieldNamingPolicy = policy;
        RebuildNamingAliases();
        return this;
    }

    void DependencyInjection.IQuerySchemaNaming.ApplyFieldNamingPolicy(JsonNamingPolicy policy) => UseFieldNamingPolicy(policy);

    private void RebuildNamingAliases() {
        this._aliasesToMember.Clear();

        if(this._fieldNamingPolicy is null) {
            return;
        }

        foreach(QueryProperty<T> property in this._propertiesByMemberName.Values) {
            string rendered = PublicName(property);

            if(string.Equals(rendered, property.ExposedName, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            bool takenByField = this._propertiesByExposedName.TryGetValue(rendered, out QueryProperty<T>? owner)
                && !string.Equals(owner.MemberName, property.MemberName, StringComparison.OrdinalIgnoreCase);

            bool takenByAlias = this._aliasesToMember.TryGetValue(rendered, out string? aliasOwner)
                && !string.Equals(aliasOwner, property.MemberName, StringComparison.OrdinalIgnoreCase);

            if(takenByField || takenByAlias) {
                throw new InvalidOperationException(
                    $"The naming policy renders '{property.ExposedName}' as '{rendered}', which already names another " +
                    $"field. Give one of them an explicit name with HasName.");
            }

            this._aliasesToMember[rendered] = property.MemberName;
        }
    }

    /// <summary>The name a field is published under: its explicit name, or its policy rendering.</summary>
    private string PublicName(QueryProperty<T> property) {
        if(property.IsExplicitlyNamed || this._fieldNamingPolicy is null) {
            return property.ExposedName;
        }

        JsonNamingPolicy policy = this._fieldNamingPolicy;
        return string.Join('.', property.ExposedName.Split('.').Select(policy.ConvertName));
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
    bool AllowEmptyString = false,
    bool IsExplicitlyNamed = false,
    string? Description = null,
    bool IsCustom = false,
    LambdaExpression? CustomPredicate = null);

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
        this._property = this._property with { ExposedName = alias.Trim(), IsExplicitlyNamed = true };
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
        if(this._property.CustomPredicate is not null) {
            // A predicate is evaluated against one value at a time, so only the operators that combine single
            // values — equality and set membership — have a meaning for it.
            QueryOperator[] supported = [QueryOperator.Equal, QueryOperator.NotEqual, QueryOperator.In, QueryOperator.NotIn];

            if(operators.Length == 0) {
                operators = supported;
            }
            else if(operators.Any(op => !supported.Contains(op))) {
                throw new InvalidOperationException(
                    $"The custom filter '{this._property.ExposedName}' is applied through a predicate, which supports " +
                    "only Equal, NotEqual, In and NotIn.");
            }
        }

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
    /// Describes the field for callers. Published alongside it in a generated document; has no effect on
    /// validation or application.
    /// </summary>
    /// <param name="description">What the field means, in the terms a caller uses.</param>
    public PropertyRuleBuilder<T, TProperty> Describe(string description) {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        this._property = this._property with { Description = description.Trim() };
        this._schema.UpdateProperty(this._property.ExposedName, this._property);
        return this;
    }

    /// <summary>
    /// Marks the property as allowed for sorting.
    /// </summary>
    public PropertyRuleBuilder<T, TProperty> AllowSort() {
        if(this._property.IsCustom) {
            throw new InvalidOperationException(
                $"'{this._property.ExposedName}' is a custom filter and has no column to sort by.");
        }

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