using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting.AspNetCore;

/// <summary>
/// Extracts a rate limiting key based on an authenticated user claim (e.g. Sub, NameIdentifier).
/// </summary>
public sealed class UserClaimKeySelector : IRateLimitKeySelector<HttpContext> {
    private readonly string _claimType;
    private readonly string _prefix;
    private readonly IRateLimitKeySelector<HttpContext> _fallbackSelector;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserClaimKeySelector"/> class with default claim type and fallback selector.
    /// </summary>
    public UserClaimKeySelector()
        : this(ClaimTypes.NameIdentifier, "user:", new ClientIpKeySelector("anonymous_ip:")) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserClaimKeySelector"/> class with a specified claim type.
    /// </summary>
    /// <param name="claimType">The type of the claim to extract.</param>
    public UserClaimKeySelector(string claimType)
        : this(claimType, "user:", new ClientIpKeySelector("anonymous_ip:")) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserClaimKeySelector"/> class with a specified claim type and key prefix.
    /// </summary>
    /// <param name="claimType">The type of the claim to extract.</param>
    /// <param name="prefix">The key prefix for scope isolation.</param>
    public UserClaimKeySelector(string claimType, string prefix)
        : this(claimType, prefix, new ClientIpKeySelector("anonymous_ip:")) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserClaimKeySelector"/> class with claim type, key prefix, and fallback selector.
    /// </summary>
    /// <param name="claimType">The type of the claim to extract.</param>
    /// <param name="prefix">The key prefix for scope isolation.</param>
    /// <param name="fallbackSelector">The fallback selector invoked when the user is unauthenticated or the claim is missing.</param>
    public UserClaimKeySelector(
        string claimType,
        string prefix,
        IRateLimitKeySelector<HttpContext> fallbackSelector) {
        Preca.ThrowIfNullOrWhiteSpace(claimType);
        Preca.ThrowIfNull(prefix);
        Preca.ThrowIfNull(fallbackSelector);

        this._claimType = claimType;
        this._prefix = prefix;
        this._fallbackSelector = fallbackSelector;
    }

    /// <inheritdoc/>
    public string GetKey(HttpContext context) {
        Preca.ThrowIfNull(context);

        Claim? claim = context.User.FindFirst(this._claimType);
        if(claim is not null && !string.IsNullOrWhiteSpace(claim.Value)) {
            return string.Concat(this._prefix, claim.Value);
        }

        return this._fallbackSelector.GetKey(context);
    }
}