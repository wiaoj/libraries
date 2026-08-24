using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting.AspNetCore;

/// <summary>
/// Extracts a rate limiting key based on an authenticated user claim (e.g. Sub, NameIdentifier).
/// Falls back to client IP if unauthenticated.
/// </summary>
public sealed class UserClaimKeySelector : IRateLimitKeySelector<HttpContext> {
    private readonly string _claimType;
    private readonly string _prefix;
    private readonly IRateLimitKeySelector<HttpContext> _fallbackSelector;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserClaimKeySelector"/> class.
    /// </summary>
    /// <param name="claimType">The type of the claim to extract. Defaults to <see cref="ClaimTypes.NameIdentifier"/>.</param>
    /// <param name="prefix">An optional key prefix for scope isolation (e.g. <c>"user:"</c>). Defaults to <c>"user:"</c>.</param>
    /// <param name="fallbackSelector">
    /// An optional fallback selector invoked when the user is unauthenticated or the claim is missing.
    /// Defaults to a <see cref="ClientIpKeySelector"/> prefixed with <c>"anonymous_ip:"</c>.
    /// </param>
    public UserClaimKeySelector(
        string claimType = ClaimTypes.NameIdentifier,
        string prefix = "user:",
        IRateLimitKeySelector<HttpContext>? fallbackSelector = null) {
        Preca.ThrowIfNullOrWhiteSpace(claimType);
        this._claimType = claimType;
        this._prefix = prefix ?? string.Empty;
        this._fallbackSelector = fallbackSelector ?? new ClientIpKeySelector("anonymous_ip:");
    }

    /// <inheritdoc />
    public string GetKey(HttpContext context) {
        Preca.ThrowIfNull(context);

        Claim? claim = context.User.FindFirst(this._claimType);
        if(claim is not null && !string.IsNullOrWhiteSpace(claim.Value)) {
            return string.Concat(this._prefix, claim.Value);
        }

        return this._fallbackSelector.GetKey(context);
    }
}