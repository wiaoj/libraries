namespace Wiaoj.RateLimiting;

/// <summary>
/// Extracts a rate-limiting key from a transport- or scenario-specific context.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam that keeps <see cref="IRateLimitAlgorithm"/> completely unaware of where a
/// key comes from. An ASP.NET Core middleware implements <c>IRateLimitKeySelector&lt;HttpContext&gt;</c>
/// (e.g. IP address or authenticated user id); a webhook delivery pipeline implements
/// <c>IRateLimitKeySelector&lt;WebhookDeliveryContext&gt;</c> (e.g. subscriber id + endpoint id).
/// Neither implementation leaks into the core package.
/// </para>
/// <para>
/// Implementations should be pure and side-effect free: given the same <typeparamref name="TContext"/>,
/// they should always produce the same key. Any randomness or environment dependence here makes
/// rate-limit decisions non-reproducible and hard to test.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The context type this selector knows how to derive a key from.</typeparam>
public interface IRateLimitKeySelector<in TContext> {
    /// <summary>
    /// Derives the rate-limiting key for the given <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The scenario-specific context to derive a key from.</param>
    /// <returns>
    /// A non-null, non-empty key. Implementations are responsible for ensuring the key is
    /// sufficiently unique (e.g. prefixed with a scope) so that unrelated consumers of the same
    /// <see cref="IRateLimitAlgorithm"/> instance don't collide on the same key space.
    /// </returns>
    string GetKey(TContext context);
}
