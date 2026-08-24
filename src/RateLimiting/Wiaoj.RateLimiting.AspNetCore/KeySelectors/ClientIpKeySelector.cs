using Microsoft.AspNetCore.Http;
using System.Net;
using Wiaoj.Preconditions;

namespace Wiaoj.RateLimiting.AspNetCore;

/// <summary>
/// Extracts a rate limiting key based on the client's IP address with zero heap allocations for formatting.
/// </summary>
public sealed class ClientIpKeySelector : IRateLimitKeySelector<HttpContext> {
    private readonly string _prefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientIpKeySelector"/> class.
    /// </summary>
    /// <param name="prefix">An optional key prefix for scope isolation (e.g. <c>"ip:"</c>). Defaults to <c>"ip:"</c>.</param>
    public ClientIpKeySelector(string prefix = "ip:") {
        this._prefix = prefix ?? string.Empty;
    }

    /// <inheritdoc />
    public string GetKey(HttpContext context) {
        Preca.ThrowIfNull(context);

        IPAddress? ip = context.Connection.RemoteIpAddress;
        if(ip is null) {
            return string.Concat(this._prefix, "unknown_ip");
        }

        // IPv6 addresses can take up to 45 chars; 48 chars stack buffer guarantees zero heap allocation
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