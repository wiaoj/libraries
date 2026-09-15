using System.Security.Cryptography;
using Wiaoj.Identifiers;

namespace Wiaoj.Identifiers.Tests.Unit;

[Identifier("usr")]
public readonly partial record struct UserId;

[Identifier("org")]
internal readonly partial record struct OrgId;

[Identifier("api_key")]
public readonly partial record struct ApiKeyId;

/// <summary>Codecs with fixed keys, so a test's expectations do not depend on randomness.</summary>
internal static class TestCodecs {
    public static readonly byte[] Key = [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

    public static readonly byte[] OtherKey = [.. Enumerable.Range(100, 32).Select(i => (byte)i)];

    public static AesIdCodec Aes(char version = '1') => new(Key, version);

    public static byte[] RandomKey() => RandomNumberGenerator.GetBytes(32);
}

/// <summary>
/// Tests that install a codec for the whole process, or assert that none is installed, run alone: an installed codec is
/// process-wide state.
/// </summary>
[CollectionDefinition(nameof(InstalledCodecCollection), DisableParallelization = true)]
public sealed class InstalledCodecCollection;
