using System.Net;
using Wiaoj.Webhooks.Security;

namespace Wiaoj.Webhooks.Tests.Unit.Security;

[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "RealWorldSsrf")]
public sealed class RealWorldSsrfPayloadsTests {

    // ────────────────────────────────────────────────────────────────────────
    // 1. REAL-WORLD CLOUD PROVIDER METADATA ENDPOINTS (MUST BE BLOCKED)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class CloudMetadataEndpoints {
        [Theory]
        [InlineData("169.254.169.254", "AWS EC2/ECS/EKS IMDS")]
        [InlineData("169.254.169.254", "Azure Instance Metadata Service")]
        [InlineData("169.254.169.254", "Google Cloud Engine Metadata")]
        [InlineData("169.254.169.254", "DigitalOcean Metadata Service")]
        [InlineData("169.254.169.254", "Oracle Cloud Infrastructure")]
        [InlineData("169.254.170.2", "AWS ECS Task Metadata")]
        [InlineData("169.254.1.1", "OpenStack Default Metadata")]
        public void IsAllowed_BlocksAllMajorCloudProviderMetadataIps(string cloudMetadataIp, string providerName) {
            IPAddress ip = IPAddress.Parse(cloudMetadataIp);
            Assert.False(WebhookIpFilter.IsAllowed(ip), $"CRITICAL: '{providerName}' metadata IP '{cloudMetadataIp}' was not blocked!");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. KUBERNETES & CONTAINER INTERNAL NETWORKS (MUST BE BLOCKED)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class ContainerAndKubernetesInternalNetworks {
        [Theory]
        [InlineData("10.96.0.1")]     // Kubernetes default Service ClusterIP (kubernetes.default.svc)
        [InlineData("10.244.0.1")]    // Flannel / Calico CNI default Pod CIDR gateway
        [InlineData("172.17.0.1")]    // Docker default bridge network gateway (docker0)
        [InlineData("172.18.0.1")]    // Docker compose custom user-defined bridge
        [InlineData("10.0.0.1")]      // Cloud VPC internal subnet gateway
        [InlineData("192.168.1.1")]   // Enterprise / Home LAN router admin gateway
        [InlineData("192.168.0.1")]   // Common internal network gateway
        [InlineData("127.0.0.1")]     // Localhost
        [InlineData("127.0.1.1")]     // Debian/Ubuntu local host mapping
        [InlineData("127.255.255.254")] // Top-of-loopback subnet
        public void IsAllowed_BlocksContainerAndInternalInfrastructureIps(string internalIp) {
            IPAddress ip = IPAddress.Parse(internalIp);
            Assert.False(WebhookIpFilter.IsAllowed(ip), $"CRITICAL VULNERABILITY: Internal infrastructure IP '{internalIp}' was not blocked!");
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. REAL-WORLD PUBLIC INTERNET WEBSITES & CDNS (MUST BE ALLOWED)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class RealWorldPublicInternetEndpoints {
        [Theory]
        // Public Anycast DNS Providers
        [InlineData("1.1.1.1")]               // Cloudflare Public DNS (IPv4)
        [InlineData("1.0.0.1")]               // Cloudflare Secondary DNS (IPv4)
        [InlineData("2606:4700:4700::1111")]  // Cloudflare Public DNS (IPv6)
        [InlineData("8.8.8.8")]               // Google Public DNS (IPv4)
        [InlineData("8.8.4.4")]               // Google Secondary DNS (IPv4)
        [InlineData("2001:4860:4860::8888")]  // Google Public DNS (IPv6)
        [InlineData("9.9.9.9")]               // Quad9 Threat-Blocking DNS (IPv4)
        [InlineData("208.67.222.222")]        // Cisco OpenDNS (IPv4)

        // Real-World Webhook Consumers & Web Services
        [InlineData("140.82.121.4")]          // GitHub.com Webhook Delivery Server
        [InlineData("151.101.1.140")]         // Fastly CDN Edge
        [InlineData("104.244.42.1")]          // Twitter / X Public Edge
        [InlineData("157.240.241.35")]        // Meta / Facebook Public Edge
        [InlineData("13.107.42.14")]          // Microsoft Public Azure Front Door
        [InlineData("54.239.28.85")]          // Amazon AWS Public Front Door
        public void IsAllowed_AllowsLegitimatePublicInternetIps(string publicIp) {
            IPAddress ip = IPAddress.Parse(publicIp);
            Assert.True(WebhookIpFilter.IsAllowed(ip), $"FALSE POSITIVE: Legitimate public IP '{publicIp}' was incorrectly blocked!");
        }
    }
}