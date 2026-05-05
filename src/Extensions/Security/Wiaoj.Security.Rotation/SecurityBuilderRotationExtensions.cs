using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using Wiaoj.Concurrency;
using Wiaoj.Security;
using Wiaoj.Security.DependencyInjection;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

public static class SecurityBuilderRotationExtensions {

    // ── Managed protector ─────────────────────────────────────────────────────

    /// <summary>
    /// Registers the full lifecycle for a single secret domain:
    /// <list type="bullet">
    ///   <item><see cref="KeyRingLoader{TContext}"/> (scoped)</item>
    ///   <item><see cref="ManagedSecretProtector{TContext}"/> (singleton, lazy via <see cref="AsyncLazy{T}"/> where T is <see cref="ISecretProtector{TContext}"/>)</item>
    ///   <item><see cref="SecurityInitializationService{TContext}"/> — pre-warms on startup</item>
    ///   <item><see cref="KeyRotationService{TContext}"/> (scoped)</item>
    ///   <item><see cref="RotationBackgroundService{TContext}"/> — periodic rotation check</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Requires that an <see cref="IEncryptionKeyStore"/> implementation and an
    /// <see cref="IMasterKeyProvider"/> are already registered.
    /// </remarks>
    public static ISecurityBuilder AddManagedProtector<TContext>(
        this ISecurityBuilder builder)
        where TContext : ISecretContext {
        IServiceCollection services = builder.Services;

        // Loader — scoped so it shares the DbContext / store lifetime.
        services.TryAddScoped<KeyRingLoader<TContext>>();

        // ManagedSecretProtector — singleton; lazy initialization via AsyncLazy.
        // No .GetAwaiter().GetResult() blocking: SecurityInitializationService pre-warms it.
        services.TryAddSingleton<ManagedSecretProtector<TContext>>(sp => {
            IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

            AsyncLazy<SecretProtector<TContext>> lazy = new(async ct => {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                KeyRingLoader<TContext> loader =
                    scope.ServiceProvider.GetRequiredService<KeyRingLoader<TContext>>();
                KeyRing<TContext> ring = await loader.LoadAsync(ct);
                return new SecretProtector<TContext>(ring);
            });

            return new ManagedSecretProtector<TContext>(lazy, scopeFactory);
        });

        // Interface alias so consumers inject ISecretProtector<TContext>.
        services.TryAddSingleton<ISecretProtector<TContext>>(
            sp => sp.GetRequiredService<ManagedSecretProtector<TContext>>());

        // Pre-warm the lazy key ring during IHostedService.StartAsync (fully async).
        services.AddHostedService<SecurityInitializationService<TContext>>();

        // Rotation orchestration — scoped to get a fresh store per cycle.
        services.TryAddScoped<KeyRotationService<TContext>>();

        // Periodic rotation check — singleton, creates its own scope per tick.
        services.AddHostedService<RotationBackgroundService<TContext>>();

        return builder;
    }

    // ── Data rotator ──────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a <see cref="IDataRotator{TContext}"/> that re-encrypts application data
    /// after a key rotation. Call after <see cref="AddManagedProtector{TContext}"/>.
    /// </summary>
    public static ISecurityBuilder AddDataRotator<TContext,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRotator>(
        this ISecurityBuilder builder)
        where TContext : ISecretContext
        where TRotator : class, IDataRotator<TContext> {
        builder.Services.TryAddScoped<IDataRotator<TContext>, TRotator>();
        return builder;
    }
}
