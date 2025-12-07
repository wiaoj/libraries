using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wiaoj.Ddd.Abstractions.ValueObjects;

namespace Wiaoj.Ddd.EntityFrameworkCore.ValueConverters;
/// <summary>
/// Converts the custom RowVersion struct to a byte array for database storage.
/// </summary>
public class RowVersionConverter : ValueConverter<RowVersion, byte[]> {
    public RowVersionConverter()
        : base(
            v => v.Value,
            v => RowVersion.From(v)) { }
}