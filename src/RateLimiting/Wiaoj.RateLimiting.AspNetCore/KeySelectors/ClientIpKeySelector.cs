using Microsoft.AspNetCore.Http;
using System.Net;
using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting.AspNetCore;

/// <summary>
/// Extracts a rate limiting key based on the client's IP address.
/// </summary>
public sealed class ClientIpKeySelector : IRateLimitKeySelector<HttpContext> {
    private readonly string _prefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientIpKeySelector"/> class with the default <c>"ip:"</c> prefix.
    /// </summary>
    public ClientIpKeySelector()
        : this("ip:") { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientIpKeySelector"/> class with a custom prefix.
    /// </summary>
    /// <param name="prefix">The key prefix for scope isolation.</param>
    public ClientIpKeySelector(string prefix) {
        Preca.ThrowIfNull(prefix);
        this._prefix = prefix;
    }

    /// <inheritdoc/>
    public string GetKey(HttpContext context) {
        Preca.ThrowIfNull(context);

        IPAddress? ip = context.Connection.RemoteIpAddress;
        if(ip is null) {
            return string.Concat(this._prefix, "unknown_ip");
        }

        Span<char> ipBuffer = stackalloc char[48];
        if(ip.TryFormat(ipBuffer, out int charsWritten)) {
            ReadOnlySpan<char> formattedIp = ipBuffer[..charsWritten];
            return string.Create(this._prefix.Length + charsWritten, (this._prefix, formattedIp.ToString()), static (span, state) => {
                state._prefix.AsSpan().CopyTo(span);
                state.Item2.AsSpan().CopyTo(span[state._prefix.Length..]);
            });
        }

        return string.Concat(this._prefix, ip.ToString());
    }
}