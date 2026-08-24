using Microsoft.AspNetCore.Http;
using System.Net;
using System.Security.Claims;
using Wiaoj.RateLimiting.AspNetCore;

namespace Wiaoj.RateLimiting.Tests.Unit.KeySelectors;

public sealed class KeySelectorTests {
    [Fact]
    public void ClientIpKeySelector_WithRemoteIp_FormatsCorrectKey() {
        ClientIpKeySelector selector = new("ip:");
        DefaultHttpContext context = new() {
            Connection = { RemoteIpAddress = IPAddress.Parse("192.168.1.50") }
        };

        string key = selector.GetKey(context);

        Assert.Equal("ip:192.168.1.50", key);
    }

    [Fact]
    public void ClientIpKeySelector_WithIpv6Loopback_FormatsCorrectKey() {
        ClientIpKeySelector selector = new("ip:");
        DefaultHttpContext context = new() {
            Connection = { RemoteIpAddress = IPAddress.IPv6Loopback }
        };

        string key = selector.GetKey(context);

        Assert.Equal("ip:::1", key);
    }

    [Fact]
    public void ClientIpKeySelector_WhenRemoteIpNull_ReturnsUnknownIpFallback() {
        ClientIpKeySelector selector = new("ip:");
        DefaultHttpContext context = new();

        string key = selector.GetKey(context);

        Assert.Equal("ip:unknown_ip", key);
    }

    [Fact]
    public void ApiKeyHeaderKeySelector_WithHeaderPresent_ReturnsApiKey() {
        ApiKeyHeaderKeySelector selector = new("X-Api-Key", "key:");
        DefaultHttpContext context = new();
        context.Request.Headers["X-Api-Key"] = "secret_token_123";

        string key = selector.GetKey(context);

        Assert.Equal("key:secret_token_123", key);
    }

    [Fact]
    public void ApiKeyHeaderKeySelector_WhenHeaderMissing_FallsBackToIpSelector() {
        ApiKeyHeaderKeySelector selector = new("X-Api-Key", "key:");
        DefaultHttpContext context = new() {
            Connection = { RemoteIpAddress = IPAddress.Parse("10.0.0.1") }
        };

        string key = selector.GetKey(context);

        Assert.Equal("anonymous_ip:10.0.0.1", key);
    }

    [Fact]
    public void UserClaimKeySelector_WithAuthenticatedUser_ReturnsClaimKey() {
        UserClaimKeySelector selector = new(ClaimTypes.NameIdentifier, "usr:");
        DefaultHttpContext context = new();
        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, "user_999")], "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        string key = selector.GetKey(context);

        Assert.Equal("usr:user_999", key);
    }

    [Fact]
    public void UserClaimKeySelector_WhenUnauthenticated_FallsBackToIpSelector() {
        UserClaimKeySelector selector = new(ClaimTypes.NameIdentifier, "usr:");
        DefaultHttpContext context = new() {
            Connection = { RemoteIpAddress = IPAddress.Parse("127.0.0.1") }
        };

        string key = selector.GetKey(context);

        Assert.Equal("anonymous_ip:127.0.0.1", key);
    }
}