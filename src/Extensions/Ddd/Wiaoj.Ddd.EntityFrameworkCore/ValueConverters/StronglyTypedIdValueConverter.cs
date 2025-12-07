using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wiaoj.Ddd.Abstractions.ValueObjects;

namespace Wiaoj.Ddd.EntityFrameworkCore.ValueConverters;
/// <summary>
/// Converts strongly-typed IDs (implementing IId) to their underlying primitive type and vice versa.
/// </summary>
public class StronglyTypedIdValueConverter<TId, TValue> : ValueConverter<TId, TValue>
    where TId : IId<TId, TValue>
    where TValue : notnull {

    public StronglyTypedIdValueConverter(ConverterMappingHints? mappingHints = null)
        : base(
            id => id.Value,
            value => CreateFromValue(value),
            mappingHints) { }

    private static TId CreateFromValue(TValue value) {
        return TId.From(value);
    }
}