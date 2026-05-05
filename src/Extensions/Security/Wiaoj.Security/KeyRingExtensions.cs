namespace Wiaoj.Security;

/// <summary>
/// Convenience extensions on <see cref="KeyRing{TContext}"/>.
/// </summary>
public static class KeyRingExtensions {

    /// <summary>
    /// Creates a <see cref="SecretProtector{TContext}"/> from this key ring.
    /// The protector takes ownership of the ring and will dispose it when the protector is disposed.
    /// </summary>
    /// <example>
    /// <code>
    /// using var protector = new KeyRingBuilder&lt;WebhookContext&gt;()
    ///     .WithCurrentKey(version: 1, masterKey)
    ///     .Build()
    ///     .CreateProtector();
    /// </code>
    /// </example>
    public static SecretProtector<TContext> CreateProtector<TContext>(this KeyRing<TContext> ring)
        where TContext : ISecretContext {
        return new(ring);
    }
}