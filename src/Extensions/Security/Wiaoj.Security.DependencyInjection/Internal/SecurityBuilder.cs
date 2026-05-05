using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Security.DependencyInjection.Internal;

/// <summary>
/// A builder for configuring the Wiaoj security system.
/// Returned by <see cref="SecurityServiceExtensions.AddWiaojSecurity"/>.
/// </summary>
/// <remarks>
/// <para>
/// This follows the same pattern as <c>AuthenticationBuilder</c> in ASP.NET Core:
/// the core package defines the builder, and each integration package
/// (EF Core, Redis, Azure Key Vault …) adds extension methods on it.
/// </para>
/// <para>
/// Dependency direction: persistence/provider packages reference
/// <c>Wiaoj.Security.DependencyInjection</c> to extend this builder —
/// the DI package never references them.
/// </para>
/// </remarks>
internal sealed class SecurityBuilder : ISecurityBuilder { 
    /// <inheritdoc/>
    public IServiceCollection Services { get; }

    internal SecurityBuilder(IServiceCollection services) {
        this.Services = services;
    }
}