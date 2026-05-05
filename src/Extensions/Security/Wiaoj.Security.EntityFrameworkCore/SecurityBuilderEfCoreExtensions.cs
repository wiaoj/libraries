using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using Wiaoj.Security;
using Wiaoj.Security.DependencyInjection;
using Wiaoj.Security.EntityFrameworkCore;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

public static class SecurityBuilderEfCoreExtensions { 
    /// <summary>
    /// Registers <see cref="EfEncryptionKeyStore{TDbContext}"/> as the
    /// <see cref="IEncryptionKeyStore"/> implementation.
    /// </summary>
    /// <typeparam name="TDbContext">
    /// Your application's <c>DbContext</c>. Must implement <see cref="IEncryptionKeyDbContext"/>.
    /// </typeparam>
    /// <remarks>
    /// Register as Scoped — <see cref="EfEncryptionKeyStore{TDbContext}"/> depends on
    /// a scoped <typeparamref name="TDbContext"/>.
    /// <para>
    /// Your <c>DbContext</c> must implement <see cref="IEncryptionKeyDbContext"/> and apply
    /// <see cref="EncryptionKeyRecordConfiguration"/> in <c>OnModelCreating</c>:
    /// <code>
    /// public class AppDbContext : DbContext, IEncryptionKeyDbContext {
    ///     public DbSet&lt;EncryptionKeyRecord&gt; EncryptionKeys { get; set; } = null!;
    ///
    ///     protected override void OnModelCreating(ModelBuilder modelBuilder) {
    ///         modelBuilder.ApplyConfiguration(new EncryptionKeyRecordConfiguration());
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static ISecurityBuilder AddEntityFrameworkKeyStore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDbContext>(
        this ISecurityBuilder builder)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext, IEncryptionKeyDbContext {
        builder.Services.TryAddScoped<IEncryptionKeyStore, EfEncryptionKeyStore<TDbContext>>();
        return builder;
    }
}
