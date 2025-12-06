using System.ComponentModel;
using System.Globalization;

namespace Wiaoj.Primitives.Snowflake;
/// <summary>
/// Provides a type converter to convert <see cref="SnowflakeId"/> objects to and from other representations.
/// Supported types: String, Long, Guid.
/// </summary>
public class SnowflakeIdTypeConverter : TypeConverter {
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) {
        return sourceType == typeof(string) || sourceType == typeof(long) || sourceType == typeof(Guid) || base.CanConvertFrom(context, sourceType);
    }

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) {
        if (value is string str)
            return SnowflakeId.Parse(str, culture);
        if (value is long l)
            return new SnowflakeId(l);
        if (value is Guid g)
            return SnowflakeId.FromGuid(g);
        return base.ConvertFrom(context, culture, value);
    }

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) {
        return destinationType == typeof(string) || destinationType == typeof(long) || destinationType == typeof(Guid) || base.CanConvertTo(context, destinationType);
    }

    /// <inheritdoc/>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) {
        if (value is SnowflakeId id) {
            if (destinationType == typeof(string))
                return id.ToString();
            if (destinationType == typeof(long))
                return id.Value;
            if (destinationType == typeof(Guid))
                return id.ToGuid();
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
