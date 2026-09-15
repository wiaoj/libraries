using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Wiaoj.Net;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Enforces an <see cref="OutboundNetworkPolicy"/> on a client registered with <c>AddHttpClient</c>.
/// </summary>
public static class OutboundNetworkPolicyHttpClientBuilderExtensions {
    /// <summary>
    /// Enforces <paramref name="policy"/> on every connection the client opens.
    /// </summary>
    /// <param name="builder">The client builder.</param>
    /// <param name="policy">The policy.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The policy is applied after all of the client's handler configuration has run, whatever order it was registered
    /// in — a <c>ConfigurePrimaryHttpMessageHandler</c> call made later still gets it, rather than silently replacing a
    /// protected handler with an unprotected one. It keeps that handler's own settings, such as
    /// <c>AllowAutoRedirect = false</c>.
    /// </para>
    /// <para>
    /// The primary handler must be a <see cref="SocketsHttpHandler"/> (the default) with no proxy; otherwise creating the
    /// client throws <see cref="InvalidOperationException"/> instead of connecting unprotected.
    /// </para>
    /// <para>
    /// Host names are resolved with the <see cref="DnsResolver"/> registered in the container, or
    /// <see cref="DnsResolver.System"/> when there is none.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddHttpClient&lt;PartnerClient&gt;()
    ///     .AddOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly with {
    ///         AllowedNetworks = [IPNetwork.Parse("10.20.0.0/16")]
    ///     });
    /// </code>
    /// </example>
    public static IHttpClientBuilder AddOutboundNetworkPolicy(this IHttpClientBuilder builder, OutboundNetworkPolicy policy) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(policy);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, OutboundNetworkPolicyFilter>());
        builder.Services.Configure<OutboundNetworkPolicyRegistry>(registry => registry.Policies[builder.Name] = policy);
        return builder;
    }

    /// <summary>
    /// Enforces <see cref="OutboundNetworkPolicy.PublicOnly"/>, adjusted by <paramref name="configure"/>, on every
    /// connection the client opens.
    /// </summary>
    /// <param name="builder">The client builder.</param>
    /// <param name="configure">Derives the policy from <see cref="OutboundNetworkPolicy.PublicOnly"/>.</param>
    /// <returns>The builder, for chaining.</returns>
    public static IHttpClientBuilder AddOutboundNetworkPolicy(this IHttpClientBuilder builder, Func<OutboundNetworkPolicy, OutboundNetworkPolicy> configure) {
        Preca.ThrowIfNull(configure);
        return builder.AddOutboundNetworkPolicy(configure(OutboundNetworkPolicy.PublicOnly));
    }
}
