using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Identifiers;

/// <summary>
/// A strongly typed, prefixed identifier over a <see cref="SnowflakeId"/>. Implemented by the source generator for every
/// struct marked with <see cref="IdentifierAttribute"/>.
/// </summary>
/// <typeparam name="TSelf">The identifier type.</typeparam>
public interface IIdentifier<TSelf> where TSelf : struct, IIdentifier<TSelf> {
    /// <summary>Gets the prefix written before every identifier of this type.</summary>
    static abstract string Prefix { get; }

    /// <summary>Gets the underlying Snowflake value — what the database stores.</summary>
    SnowflakeId Value { get; }

    /// <summary>Creates an identifier from its underlying Snowflake value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The identifier.</returns>
    static abstract TSelf From(SnowflakeId value);
}
