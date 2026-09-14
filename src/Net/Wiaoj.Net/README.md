# Wiaoj.Net

Small building blocks on top of `System.Net`. The first one decides which IP addresses outbound connections may reach, and enforces that decision where the socket is opened. This is the defence against **server-side request forgery (SSRF)**: a URL supplied by a user or by another service must not become a request to `169.254.169.254` (cloud metadata), `localhost`, or your internal network.

## Installation

```bash
dotnet add package Wiaoj.Net
```

## Usage

```csharp
// Only public addresses
services.AddHttpClient<WebhookSender>()
    .AddOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly);

// Public addresses, plus our own internal network
services.AddHttpClient<PartnerClient>()
    .AddOutboundNetworkPolicy(policy => policy with {
        AllowedNetworks = [IPNetwork.Parse("10.20.0.0/16")]
    });
```

Without dependency injection:

```csharp
HttpClient client = new(new SocketsHttpHandler().UseOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly));
```

A refused connection throws `HttpRequestException`, with an `OutboundNetworkPolicyException` as its inner exception. The message names the host and port but not the addresses it resolved to, so it gives no map of the internal network.

## The three parts

| Type | What it does | Shape |
| --- | --- | --- |
| `IPAddressClassifier.Classify(address)` | Returns an `IPAddressScope`: `Public`, `Private`, `Loopback`, `LinkLocal`, `CarrierGradeNat`, `Documentation`, … | Static, pure |
| `OutboundNetworkPolicy` | Decides whether an address is allowed | Immutable record, derived with `with` |
| `UseOutboundNetworkPolicy` / `AddOutboundNetworkPolicy` | Enforces the policy on the connections a `SocketsHttpHandler` opens | Handler / `IHttpClientBuilder` extension |

### How the policy decides

1. **Blocked:** if the address, or the IPv4 address it carries, is in `BlockedNetworks`, it is refused.
2. **Allowed exception:** if the address is in `AllowedNetworks`, it is allowed.
3. **Scope:** otherwise it is allowed only when its scope is in `AllowedScopes`.

Presets: `PublicOnly` (the default) and `Unrestricted` (for development).

### IPv4 hidden inside IPv6

A check that looks only at the literal address can be bypassed:

- `::ffff:169.254.169.254` (IPv4-mapped)
- `2002:a9fe:a9fe::` (6to4)
- `64:ff9b::a9fe:a9fe` (NAT64)
- Teredo addresses

All of these reach the metadata endpoint. The classifier recognises the IPv4 address inside and classifies the address by it.

## Why checking at connection time matters

If a URL's host is resolved, checked, and then connected to separately, DNS can return a different address the second time (**DNS rebinding**). Here the handler resolves the host itself and connects to the address it checked, so there is no second lookup.

`AddOutboundNetworkPolicy` applies the policy **after** all of the client's handler configuration has run. A later `ConfigurePrimaryHttpMessageHandler` call can't silently replace the protected handler, and that handler's own settings, such as `AllowAutoRedirect = false`, are kept.

## Proxies

Behind a proxy, the connection goes to the proxy, and the proxy reaches the destination. The client never sees the destination's address, so it can't check it:

- An explicitly configured proxy is refused with `InvalidOperationException`.
- The environment proxy (`HTTPS_PROXY`) is turned off (`UseProxy = false`), so it can't silently take over the connection.

If your traffic must go through a proxy, enforce egress rules **at the proxy** (for example Smokescreen or Squid ACLs).

## Requirements

The primary handler must be a `SocketsHttpHandler`, which is the default in `IHttpClientFactory`. With any other handler, creating the client throws, rather than sending requests unprotected.
