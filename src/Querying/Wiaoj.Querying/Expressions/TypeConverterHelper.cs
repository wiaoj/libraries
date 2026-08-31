using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Wiaoj.Querying.Expressions;

/// <summary>
/// Provides safe, exception-free, and Native AOT-compliant type conversion for filter values.
/// </summary>
internal static class TypeConverterHelper {
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

        return false;
    }
}