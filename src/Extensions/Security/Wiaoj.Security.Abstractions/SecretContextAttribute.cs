using System.Reflection;

namespace Wiaoj.Security;

/// <summary>Sets the persistent key-store name and authenticated encryption context.</summary>
/// <remarks>Use a unique, stable identifier. Before renaming an existing context class, specify its
/// original short name. Changing the identifier requires migrating existing keys and ciphertext.</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = false)]
public sealed class SecretContextAttribute : Attribute {
    /// <summary>Creates a persistent context identifier.</summary>
    public SecretContextAttribute(string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The persistent context identifier.</summary>
    public string Name { get; }
}

/// <summary>Resolves the identifier shared by key storage and authenticated encryption.</summary>
public static class SecretContextName<TContext> where TContext : ISecretContext {
    /// <summary>The explicit identifier, or the legacy short type name when unspecified.</summary>
    public static string Value { get; } = typeof(TContext).GetCustomAttribute<SecretContextAttribute>()?.Name ?? typeof(TContext).Name;
}
