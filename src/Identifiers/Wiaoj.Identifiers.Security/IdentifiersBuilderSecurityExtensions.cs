using Wiaoj.Identifiers.Security;
using Wiaoj.Preconditions;
using Wiaoj.Security;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>Keys identifier encryption by a Wiaoj.Security key ring.</summary>
public static class IdentifiersBuilderSecurityExtensions {
    /// <summary>
    /// Uses <see cref="KeyRingIdCodec{TContext}"/>: identifiers are encrypted under the key ring of
    /// <typeparamref name="TContext"/>, and follow its rotation.
    /// </summary>
    /// <typeparam name="TContext">The secret domain whose key ring keys identifiers.</typeparam>
    /// <param name="builder">The identifiers builder.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>
    /// Requires an <see cref="ISubkeyDeriver{TContext}"/>, which <c>AddManagedProtector&lt;TContext&gt;()</c> registers.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddWiaojSecurity()
    ///     .AddEnvironmentMasterKey()
    ///     .AddEntityFrameworkKeyStore&lt;AppDbContext&gt;()
    ///     .AddManagedProtector&lt;IdentifierContext&gt;();
    ///
    /// services.AddIdentifiers().UseKeyRingCodec&lt;IdentifierContext&gt;();
    /// </code>
    /// </example>
    public static IdentifiersBuilder UseKeyRingCodec<TContext>(this IdentifiersBuilder builder) where TContext : ISecretContext {
        Preca.ThrowIfNull(builder);
        return builder.UseCodec(static services => new KeyRingIdCodec<TContext>(services.GetRequiredService<ISubkeyDeriver<TContext>>()));
    }
}
