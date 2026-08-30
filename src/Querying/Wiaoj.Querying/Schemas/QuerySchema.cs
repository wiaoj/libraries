namespace Wiaoj.Querying;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;

/// <summary>
/// Defines the filtering, searching, and sorting rules for a target entity with Native AOT compatibility.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class QuerySchema<T> {
    private readonly Dictionary<string, PropertyFilterDescriptor<T>> _filterDescriptors = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Expression<Func<T, string>>> _searchSelectors = [];

    /// <summary>
    /// Configures an allowed property for filtering, capturing property expressions at configuration time for AOT safety.
    /// </summary>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="propertySelector">The property selector expression (e.g. <c>p => p.Price</c>).</param>
    public QuerySchema<T> AllowFilter<TProperty>(Expression<Func<T, TProperty>> propertySelector) {
        var propertyName = GetPropertyName(propertySelector);
        var descriptor = new PropertyFilterDescriptor<T>(
            PropertyName: propertyName,
            PropertyType: typeof(TProperty),
            PropertySelector: propertySelector.Body,
            Parameter: propertySelector.Parameters[0],
            ValueParser: rawValue => ParseValue<TProperty>(rawValue));

        _filterDescriptors[propertyName] = descriptor;
        return this;
    }

    /// <summary>
    /// Configures allowed properties for filtering in bulk.
    /// </summary>
    public QuerySchema<T> AllowFilter<T1, T2>(Expression<Func<T, T1>> p1, Expression<Func<T, T2>> p2) {
        AllowFilter(p1);
        AllowFilter(p2);
        return this;
    }

    /// <summary>
    /// Configures allowed properties for filtering in bulk.
    /// </summary>
    public QuerySchema<T> AllowFilter<T1, T2, T3>(Expression<Func<T, T1>> p1, Expression<Func<T, T2>> p2, Expression<Func<T, T3>> p3) {
        AllowFilter(p1);
        AllowFilter(p2);
        AllowFilter(p3);
        return this;
    }

    /// <summary>
    /// Configures properties to be included in free-text search queries (<c>q=term</c>).
    /// </summary>
    public QuerySchema<T> SearchIn(params Expression<Func<T, string>>[] propertySelectors) {
        _searchSelectors.AddRange(propertySelectors);
        return this;
    }

    internal bool TryGetFilterDescriptor(string fieldName, [NotNullWhen(true)] out PropertyFilterDescriptor<T>? descriptor) {
        return _filterDescriptors.TryGetValue(fieldName, out descriptor);
    }

    internal IReadOnlyList<Expression<Func<T, string>>> SearchSelectors => _searchSelectors;

    private static string GetPropertyName<TProperty>(Expression<Func<T, TProperty>> expression) {
        if(expression.Body is MemberExpression member) {
            return member.Member.Name;
        }

        if(expression.Body is UnaryExpression { Operand: MemberExpression unaryMember }) {
            return unaryMember.Member.Name;
        }

        throw new ArgumentException("Expression must point directly to a member property.", nameof(expression));
    }

    private static object? ParseValue<TProperty>(string rawValue) {
        var targetType = typeof(TProperty);
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if(underlyingType == typeof(string)) return rawValue;
        if(underlyingType == typeof(decimal)) return decimal.Parse(rawValue, CultureInfo.InvariantCulture);
        if(underlyingType == typeof(int)) return int.Parse(rawValue, CultureInfo.InvariantCulture);
        if(underlyingType == typeof(long)) return long.Parse(rawValue, CultureInfo.InvariantCulture);
        if(underlyingType == typeof(double)) return double.Parse(rawValue, CultureInfo.InvariantCulture);
        if(underlyingType == typeof(bool)) return bool.Parse(rawValue);
        if(underlyingType == typeof(Guid)) return Guid.Parse(rawValue);
        if(underlyingType == typeof(DateTime)) return DateTime.Parse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        return Convert.ChangeType(rawValue, underlyingType, CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Holds pre-extracted expression metadata for a filterable property.
/// </summary>
internal sealed record PropertyFilterDescriptor<T>(
    string PropertyName,
    Type PropertyType,
    Expression PropertySelector,
    ParameterExpression Parameter,
    Func<string, object?> ValueParser);