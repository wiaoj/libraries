namespace Wiaoj.Identifiers;

/// <summary>
/// Generates a strongly typed, prefixed identifier for the <c>readonly partial record struct</c> it is applied to.
/// </summary>
/// <remarks>
/// <para>
/// The struct wraps a <see cref="Primitives.Snowflake.SnowflakeId"/> and is written publicly as <c>prefix_…</c> by the
/// installed <see cref="IdCodec"/>:
/// </para>
/// <code>
/// [Identifier("usr")]
/// public readonly partial record struct UserId;
///
/// UserId id = UserId.New();
/// string text = id.ToString();      // "usr_…"
/// UserId back = UserId.Parse(text);
/// </code>
/// <para>
/// The prefix is 1–32 characters: lowercase ASCII letters and digits, starting with a letter, optionally separated by
/// single underscores (<c>usr</c>, <c>api_key</c>). Each prefix may be used by one identifier per compilation.
/// </para>
/// </remarks>
/// <param name="prefix">The prefix written before every identifier of this type.</param>
[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class IdentifierAttribute(string prefix) : Attribute {
    /// <summary>Gets the prefix written before every identifier of this type.</summary>
    public string Prefix { get; } = prefix;
}
