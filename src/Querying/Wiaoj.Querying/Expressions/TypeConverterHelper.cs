using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Wiaoj.Querying.Expressions;

/// <summary>
/// Provides safe, exception-free, and Native AOT-compliant type conversion for filter values.
/// </summary>
internal static class TypeConverterHelper {
    /// <summary>
    /// Caches the resolved <c>TryParse</c> method for custom types that implement <c>IParsable&lt;T&gt;</c>
    /// or expose a compatible static <c>TryParse(string?, out T)</c> method.
    /// A <see langword="null"/> value indicates no suitable method was found.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, TryParseDelegate?> _tryParseCache = new();

    /// <summary>
    /// Delegate shape that wraps a static <c>TryParse</c> call for a specific type.
    /// </summary>
    private delegate bool TryParseDelegate(string rawValue, out object? result);

    public static bool TryConvertValue(
        string? rawValue,
        Type targetType,
        [NotNullWhen(true)] out object? result) {
        result = null;
        if(rawValue is null) {
            return false;
        }

        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if(underlyingType == typeof(string)) {
            result = rawValue;
            return true;
        }

        if(underlyingType == typeof(int)) {
            if(int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val)) {
                result = val;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(long)) {
            if(long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val)) {
                result = val;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(decimal)) {
            if(decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var val)) {
                result = val;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(double)) {
            if(double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var val)) {
                result = val;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(float)) {
            if(float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var val)) {
                result = val;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(bool)) {
            if(bool.TryParse(rawValue, out var val)) {
                result = val;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(Guid)) {
            if(Guid.TryParse(rawValue, out var guid)) {
                result = guid;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(DateTime)) {
            if(DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)) {
                result = dt;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(DateTimeOffset)) {
            if(DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)) {
                result = dto;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(DateOnly)) {
            if(DateOnly.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dOnly)) {
                result = dOnly;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(TimeOnly)) {
            if(TimeOnly.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var tOnly)) {
                result = tOnly;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(TimeSpan)) {
            if(TimeSpan.TryParse(rawValue, CultureInfo.InvariantCulture, out var ts)) {
                result = ts;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(short)) {
            if(short.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val)) {
                result = val;
                return true;
            }
            return false;
        }

        if(underlyingType == typeof(byte)) {
            if(byte.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val)) {
                result = val;
                return true;
            }
            return false;
        }

        if(underlyingType.IsEnum) {
            if(Enum.TryParse(underlyingType, rawValue, ignoreCase: true, out var enumVal)) {
                result = enumVal;
                return true;
            }
            return false;
        }

        // Fallback 1: static TryParse method (IParsable<T> or convention-based)
        if(TryParseViaStaticMethod(rawValue, underlyingType, out result)) {
            return true;
        }

        // Fallback 2: TypeConverter (covers types with [TypeConverter] attribute)
        if(TryConvertViaTypeConverter(rawValue, underlyingType, out result)) {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Caches the resolved <see cref="TypeConverter"/> instance per type.
    /// A <see langword="null"/> value indicates no suitable converter was found.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, TypeConverter?> _typeConverterCache = new();

    /// <summary>
    /// Attempts to parse <paramref name="rawValue"/> by discovering and invoking a static
    /// <c>TryParse(string?, IFormatProvider?, out T)</c> or <c>TryParse(string?, out T)</c>
    /// method on <paramref name="targetType"/>. The resolved method is cached per type.
    /// </summary>
    private static bool TryParseViaStaticMethod(
        string rawValue,
        Type targetType,
        [NotNullWhen(true)] out object? result) {
        result = null;

        TryParseDelegate? parser = _tryParseCache.GetOrAdd(targetType, CreateTryParseDelegate);

        if(parser is null) {
            return false;
        }

        return parser(rawValue, out result);
    }

    [UnconditionalSuppressMessage("AOT", "IL2070",
        Justification = "The DynamicallyAccessedMembers annotation is propagated from the calling method's targetType parameter.")]
    private static TryParseDelegate? CreateTryParseDelegate(Type type) {
        // 1. IParsable<T> pattern: TryParse(string?, IFormatProvider?, out T)
        MethodInfo? method = type.GetMethod(
            "TryParse",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
            binder: null,
            types: [typeof(string), typeof(IFormatProvider), type.MakeByRefType()],
            modifiers: null);

        if(method is not null) {
            return (string raw, out object? res) => {
                object?[] args = [raw, CultureInfo.InvariantCulture, null];
                bool success = (bool)method.Invoke(null, args)!;
                res = success ? args[2] : null;
                return success;
            };
        }

        // 2. Simple pattern: TryParse(string?, out T)
        method = type.GetMethod(
            "TryParse",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
            binder: null,
            types: [typeof(string), type.MakeByRefType()],
            modifiers: null);

        if(method is not null) {
            return (string raw, out object? res) => {
                object?[] args = [raw, null];
                bool success = (bool)method.Invoke(null, args)!;
                res = success ? args[1] : null;
                return success;
            };
        }

        return null;
    }

    /// <summary>
    /// Attempts to convert <paramref name="rawValue"/> using a <see cref="TypeConverter"/>
    /// discovered from the <see cref="TypeConverterAttribute"/> on <paramref name="targetType"/>.
    /// The resolved converter is cached per type.
    /// </summary>
    private static bool TryConvertViaTypeConverter(string rawValue, Type targetType, [NotNullWhen(true)] out object? result) {
        result = null;

        TypeConverter? converter = _typeConverterCache.GetOrAdd(targetType, static type => {
            TypeConverterAttribute? attr = (TypeConverterAttribute?)Attribute.GetCustomAttribute(type, typeof(TypeConverterAttribute));
            if(attr is null || string.IsNullOrEmpty(attr.ConverterTypeName)) {
                return null;
            }

            Type? converterType = Type.GetType(attr.ConverterTypeName);
            if(converterType is null) {
                return null;
            }

            if(Activator.CreateInstance(converterType) is TypeConverter tc && tc.CanConvertFrom(typeof(string))) {
                return tc;
            }

            return null;
        });

        if(converter is null) {
            return false;
        }

        try {
            result = converter.ConvertFromInvariantString(rawValue);
            return result is not null;
        }
        catch {
            return false;
        }
    }
}