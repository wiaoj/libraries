using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.DependencyInjection;

public class BloomFilterRegistrationTests {
    private sealed record UserBlacklistTag;
    private sealed record IpWhitelistTag;

    public sealed class ServiceCollectionExtensions {
        [Fact]
        public void Should_ResolveTypedBloomFilters_ViaMarkerTags() {
            // Arrange
            ServiceCollection services = new();
            services.AddBloomFilter(builder => {
                builder.AddFilter<UserBlacklistTag>("user-blacklist", 10_000, 0.01);
                builder.AddFilter<IpWhitelistTag>("ip-whitelist", 5_000, 0.05);
            });

            using ServiceProvider sp = services.BuildServiceProvider();

            // Act
            IBloomFilter<UserBlacklistTag> userFilter = sp.GetRequiredService<IBloomFilter<UserBlacklistTag>>();
            IBloomFilter<IpWhitelistTag> ipFilter = sp.GetRequiredService<IBloomFilter<IpWhitelistTag>>();

            // Assert
            Assert.NotNull(userFilter);
            Assert.NotNull(ipFilter);
            Assert.Equal("user-blacklist", userFilter.Name);
            Assert.Equal("ip-whitelist", ipFilter.Name);

            // Verify independent state
            userFilter.Add("banned_user_123");
            Assert.True(userFilter.Contains("banned_user_123"));
            Assert.False(ipFilter.Contains("banned_user_123"));
        }

        [Fact]
        public void Should_PopulateOptions_When_ConfiguredViaFluentBuilder() {
            // Arrange
            ServiceCollection services = new();
            services.AddBloomFilter(builder => {
                builder.AddFilter("custom-filter", 50_000, 0.001);
            });

            using ServiceProvider sp = services.BuildServiceProvider();

            // Act
            IOptions<BloomFilterOptions> options = sp.GetRequiredService<IOptions<BloomFilterOptions>>();

            // Assert
            Assert.True(options.Value.Filters.ContainsKey("custom-filter"));
            Assert.Equal(50_000, options.Value.Filters["custom-filter"].ExpectedItems);
            Assert.Equal(0.001, options.Value.Filters["custom-filter"].ErrorRate);
        }
    }

    public sealed class RegistryMethods {
        [Fact]
        public void Should_PreventDuplicateEntries_When_SameFilterIsRegisteredMultipleTimes() {
            // Arrange
            BloomFilterRegistry registry = new();
            FakeBloomFilter filter = new("unique-filter");

            // Act
            registry.Register(filter);
            registry.Register(filter);

            // Assert: Registry deduplicates by filter name
            Assert.Single(registry.GetAll());
        }
    }
}