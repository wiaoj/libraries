using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Identifiers.EntityFrameworkCore;

/// <summary>
/// Stores an identifier as its Snowflake value, a <see cref="long"/> (<c>bigint</c>).
/// </summary>
/// <remarks>
/// The database holds the value, never the text a codec writes: rows stay readable whatever codec or key the
/// application uses, and changing the key changes nothing stored.
/// </remarks>
/// <typeparam name="TId">The identifier type.</typeparam>
public sealed class IdentifierValueConverter<TId> : ValueConverter<TId, long> where TId : struct, IIdentifier<TId> {
    /// <summary>Creates the converter.</summary>
    public IdentifierValueConverter()
        : base(id => id.Value.Value, value => FromValue(value)) {
    }

    // Expression trees cannot call a static abstract member directly, so the conversion goes through a method.
    private static TId FromValue(long value) => TId.From(new SnowflakeId(value));
}

/// <summary>Generates a new identifier with <c>TId.New()</c> for a key that has none when it is added.</summary>
/// <typeparam name="TId">The identifier type.</typeparam>
public sealed class IdentifierValueGenerator<TId> : ValueGenerator<TId> where TId : struct, IIdentifier<TId> {
    /// <summary>Gets <see langword="false"/>: the value is final and saved as it is.</summary>
    public override bool GeneratesTemporaryValues => false;

    /// <inheritdoc/>
    public override TId Next(EntityEntry entry) => TId.From(SnowflakeId.NewId());
}
