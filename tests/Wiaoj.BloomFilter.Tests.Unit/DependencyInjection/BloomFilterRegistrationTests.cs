using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Storage;
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

        [Fact]
        public void Should_PropagateOptions_When_ConfiguredDirectlyViaBuilderOptions() {
            // Arrange
            ServiceCollection services = new();
            services.AddBloomFilter(builder => {
                builder.Options.Lifecycle.AutoReseed = false;
                builder.Options.Lifecycle.AutoSaveInterval = TimeSpan.FromMinutes(42);
                builder.Options.Lifecycle.ShardingThresholdBytes = 100 * 1024;
            });

            using ServiceProvider sp = services.BuildServiceProvider();

            // Act
            IOptions<BloomFilterOptions> options = sp.GetRequiredService<IOptions<BloomFilterOptions>>();

            // Assert
            Assert.False(options.Value.Lifecycle.AutoReseed);
            Assert.Equal(TimeSpan.FromMinutes(42), options.Value.Lifecycle.AutoSaveInterval);
            Assert.Equal(100 * 1024, options.Value.Lifecycle.ShardingThresholdBytes);
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

    public sealed class StorageRegistrationMethods {
        [Fact]
        public void Should_RegisterFileSystemStorage_WithDefaultOptions_When_ParameterlessOverloadUsed() {
            ServiceCollection services = new();
            services.AddBloomFilter(builder => {
                builder.UseFileSystemStorage();
            });

            using ServiceProvider sp = services.BuildServiceProvider();
            IBloomFilterStorage storage = sp.GetRequiredService<IBloomFilterStorage>();
            IOptions<FileSystemStorageOptions> options = sp.GetRequiredService<IOptions<FileSystemStorageOptions>>();

            Assert.IsType<FileSystemBloomFilterStorage>(storage);
            Assert.Equal("BloomData", options.Value.Path);
            Assert.False(options.Value.EnableCompression);
            Assert.Equal(81920, options.Value.BufferSizeBytes);
            Assert.True(options.Value.IgnoreErrors);
        }

        [Fact]
        public void Should_RegisterFileSystemStorage_WithCustomPath_When_PathOverloadUsed() {
            ServiceCollection services = new();
            services.AddBloomFilter(builder => {
                builder.UseFileSystemStorage("/custom/bloom/dir");
            });

            using ServiceProvider sp = services.BuildServiceProvider();
            IBloomFilterStorage storage = sp.GetRequiredService<IBloomFilterStorage>();
            IOptions<FileSystemStorageOptions> options = sp.GetRequiredService<IOptions<FileSystemStorageOptions>>();

            Assert.IsType<FileSystemBloomFilterStorage>(storage);
            Assert.Equal("/custom/bloom/dir", options.Value.Path);
        }

        [Fact]
        public void Should_RegisterFileSystemStorage_WithConfiguredOptions_When_ActionOverloadUsed() {
            ServiceCollection services = new();
            services.AddBloomFilter(builder => {
                builder.UseFileSystemStorage(opt => {
                    opt.Path = "App_Data/Filters";
                    opt.EnableCompression = true;
                    opt.BufferSizeBytes = 16384;
                    opt.IgnoreErrors = false;
                });
            });

            using ServiceProvider sp = services.BuildServiceProvider();
            IBloomFilterStorage storage = sp.GetRequiredService<IBloomFilterStorage>();
            IOptions<FileSystemStorageOptions> options = sp.GetRequiredService<IOptions<FileSystemStorageOptions>>();

            Assert.IsType<FileSystemBloomFilterStorage>(storage);
            Assert.Equal("App_Data/Filters", options.Value.Path);
            Assert.True(options.Value.EnableCompression);
            Assert.Equal(16384, options.Value.BufferSizeBytes);
            Assert.False(options.Value.IgnoreErrors);
        }

        [Fact]
        public void Should_RegisterCustomStorage_When_UseStorageGenericMethodUsed() {
            ServiceCollection services = new();
            services.AddBloomFilter(builder => {
                builder.UseStorage<FakeBloomFilterStorage>();
            });

            using ServiceProvider sp = services.BuildServiceProvider();
            IBloomFilterStorage storage = sp.GetRequiredService<IBloomFilterStorage>();

            Assert.IsType<FakeBloomFilterStorage>(storage);
        }

        [Fact]
        public void Should_ThrowArgumentNullException_When_InvalidArgumentsPassed() {
            IBloomFilterBuilder builder = null!;
            ServiceCollection services = new();
            services.AddBloomFilter(b => builder = b);

            Assert.NotNull(builder);
            Assert.ThrowsAny<ArgumentNullException>(() => ((IBloomFilterBuilder)null!).UseFileSystemStorage());
            Assert.ThrowsAny<ArgumentException>(() => { builder.UseFileSystemStorage(""); });
            Assert.ThrowsAny<ArgumentNullException>(() => { builder.UseFileSystemStorage((Action<FileSystemStorageOptions>)null!); });
            Assert.ThrowsAny<ArgumentNullException>(() => ((IBloomFilterBuilder)null!).UseStorage<FakeBloomFilterStorage>());
        }
    }
}