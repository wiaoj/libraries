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

### Checking a URL when it is registered

To validate a URL someone supplied before any request is made, for example when a webhook endpoint is registered, check its host. The check returns a result, not an exception:

```csharp
OutboundHostCheck check = await OutboundNetworkPolicy.PublicOnly.CheckHostAsync(new Uri(url), ct);

switch(check.Status) {
    case OutboundHostStatus.Allowed:      /* register it */ break;
    case OutboundHostStatus.Refused:      /* "this address is not allowed" */ break;
    case OutboundHostStatus.Unresolvable: /* "this host does not exist" — check.ResolutionError */ break;
}
```

This is an early answer, not the protection. DNS can answer differently by the time a request is sent, so the connection-time enforcement below is still what stops the request.
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

Presets:

| Preset | Addresses | Ports |
| --- | --- | --- |
| `PublicOnly` (the default) | Public | Any |
| `WebOnly` | Public | 80 and 443 |
| `Unrestricted` (for development) | Any | Any |

### Ports

SSRF often targets an internal service by its port as much as by its address: Redis on 6379, the Docker API on 2375, SSH on 22. Ports are checked before addresses:

- **`BlockedPorts`:** refused whatever else allows them.
- **`AllowedPorts`:** when set, only these ports are allowed. `null` (the default) allows any port.

```csharp
// A webhook receiver on 8443 as well as the web ports
OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { AllowedPorts = new HashSet<int> { 80, 443, 8443 } };
```

`PublicOnly` keeps allowing any port, because webhook receivers legitimately listen on ports such as 8443.

- **At connection time:** a refused port is refused before the host is resolved, with `OutboundNetworkPolicyException` whose `Reason` is `Port`.
- **`CheckHostAsync(Uri)`:** uses the URL's explicit port or its scheme's default (80 for http, 443 for https), and returns `Refused` with `RefusalReason` `Port`. A host name alone has no port, so `CheckHostAsync(string)` applies only the address rules.

### IPv4 hidden inside IPv6

A check that looks only at the literal address can be bypassed. Every address below reaches the cloud metadata endpoint `169.254.169.254`, and each one is recognised:

| Form | Example | RFC |
| --- | --- | --- |
| IPv4-mapped | `::ffff:169.254.169.254` | 4291 |
| 6to4 | `2002:a9fe:a9fe::` | 3056 |
| NAT64, well-known prefix | `64:ff9b::a9fe:a9fe` | 6052 |
| IPv4-translated (SIIT) | `::ffff:0:a9fe:a9fe` | 2765 |
| Teredo | `2001:0:…:5601:5601` (inverted client address) | 4380 |
| ISATAP interface identifier, under any prefix | `2606:4700::5efe:a9fe:a9fe` | 5214 |

The classifier extracts the IPv4 address and classifies the whole address by it. When one address carries two IPv4 addresses (for example a 6to4 prefix with an ISATAP identifier), the more restrictive one decides.

The **local-use translation prefix `64:ff9b:1::/48`** (RFC 8215) is `Reserved` as a whole. It translates to addresses inside the operator's network with an embedding the RFC leaves undefined, so its IPv4 address can't be extracted. IANA lists the prefix as not globally reachable.

**Allowed networks and embedded addresses:** an `AllowedNetworks` entry also covers a *translated* address (NAT64 or SIIT) whose IPv4 address is inside it, because the translator delivers to that IPv4 host. A *tunnelled* address (6to4, Teredo, ISATAP) carries a tunnel endpoint, not the host it reaches, so it is not covered. Blocked networks are checked against every embedded address.

**Limitation:** a NAT64 translator with a *network-specific* prefix taken from an operator's own global address space (RFC 6052) looks like any public address. If your network has one, add its prefix to `BlockedNetworks`.
## Why checking at connection time matters

If a URL's host is resolved, checked, and then connected to separately, DNS can return a different address the second time (**DNS rebinding**). Here the handler resolves the host itself and connects to the address it checked, so there is no second lookup.

`AddOutboundNetworkPolicy` applies the policy **after** all of the client's handler configuration has run. A later `ConfigurePrimaryHttpMessageHandler` call can't silently replace the protected handler, and that handler's own settings, such as `AllowAutoRedirect = false`, are kept.

## Hosts with several addresses

A host often resolves to several addresses, for example an IPv6 and an IPv4 one. Addresses the policy refuses are dropped first and never attempted. The allowed ones are connected to as **Happy Eyeballs v2 (RFC 8305)** describes:

- **Order:** address families are interleaved, starting with the family of the first address the resolver returned (IPv6, IPv4, IPv6, …).
- **Racing:** each attempt gets a 250 ms head start (the delay RFC 8305 recommends). If it hasn't connected by then, the next attempt starts alongside it, and an attempt that fails starts the next one at once.
- **Winner:** the first connection is used. The other attempts are cancelled, and a socket one of them still opens is closed.
- **Failure:** the request fails only when every attempt has failed, with the last attempt's `SocketException`. The handler's `ConnectTimeout` bounds all attempts together.

Without racing, an IPv6 path that silently drops packets would use up the whole connect timeout, and the healthy IPv4 address would never be tried.
## DNS resolution

Host names are resolved with a `DnsResolver`. The default is `DnsResolver.System`, the operating system resolver. To use a corporate resolver, or a fake one in tests, register a replacement. `AddOutboundNetworkPolicy` and Webhooks use the registered resolver automatically:

```csharp
services.AddSingleton<DnsResolver, ConsulDnsResolver>();

sealed class ConsulDnsResolver : DnsResolver {
    public override ValueTask<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => /* … */;
}
```

Without dependency injection, pass the resolver to `UseOutboundNetworkPolicy(policy, resolver)` or `CheckHostAsync(host, resolver)`.

- **IP literals** never reach the resolver; the policy decides them directly.
- **Unresolvable hosts:** throw `SocketException` for a host that cannot be resolved, as the system resolver does. `CheckHostAsync` reports it as `Unresolvable`, and any other exception propagates.
- **Why an abstract class:** like `TimeProvider`, so members added later can be `virtual` without breaking existing implementations.
## Proxies

Behind a proxy, the connection goes to the proxy, and the proxy reaches the destination. The client never sees the destination's address, so it can't check it:

- An explicitly configured proxy is refused with `InvalidOperationException`.
- The environment proxy (`HTTPS_PROXY`) is turned off (`UseProxy = false`), so it can't silently take over the connection.

If your traffic must go through a proxy, enforce egress rules **at the proxy** (for example Smokescreen or Squid ACLs).

## Requirements

The primary handler must be a `SocketsHttpHandler`, which is the default in `IHttpClientFactory`. With any other handler, creating the client throws, rather than sending requests unprotected.
