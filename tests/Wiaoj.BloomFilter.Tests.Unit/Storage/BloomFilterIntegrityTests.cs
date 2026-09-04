using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.ObjectPool.Testing;

namespace Wiaoj.BloomFilter.Tests.Unit.Storage;

public class BloomFilterIntegrityTests {
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    internal BloomFilterContext CreateContext(FakeBloomFilterStorage storage, bool enableIntegrityCheck = true) {
        BloomFilterOptions options = new();
        options.Lifecycle.EnableIntegrityCheck = enableIntegrityCheck;

        return new BloomFilterContext(
            Storage: storage,
            MemoryStreamPool: new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
            Logger: NullLogger.Instance,
            Options: options,
            TimeProvider: TimeProvider.System,
            ConfigFactory: this._configFactory
        );
    }

    public sealed class CorruptionDetection : BloomFilterIntegrityTests {
        [Fact]
        public async Task Should_ThrowDataIntegrityException_When_BitArrayDataIsCorrupted() {
            // Arrange
            FakeBloomFilterStorage storage = new();
            BloomFilterContext context = CreateContext(storage, enableIntegrityCheck: true);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("corrupted-bits"), 2_000, 0.01);

            using(InMemoryBloomFilter originalFilter = new(config, context)) {
                originalFilter.Add("test-element-1"u8);
                await originalFilter.SaveAsync(TestContext.Current.CancellationToken);
            }

            (BloomFilterConfiguration? Config, Stream DataStream)? loadResult = await storage.LoadStreamAsync(config.Name.Value, TestContext.Current.CancellationToken);
            Assert.NotNull(loadResult);

            byte[] payload;
            using(MemoryStream ms = new()) {
                await loadResult.Value.DataStream.CopyToAsync(ms, TestContext.Current.CancellationToken);
                payload = ms.ToArray();
            }

            payload[^1] ^= 0xFF; // Flip bits in the payload

            using(MemoryStream corruptedStream = new(payload)) {
                await storage.SaveAsync(config.Name.Value, config, corruptedStream, TestContext.Current.CancellationToken);
            }

            // Act & Assert
            using InMemoryBloomFilter reloadedFilter = new(config, context);
            await Assert.ThrowsAsync<DataIntegrityException>(() => reloadedFilter.ReloadAsync(TestContext.Current.CancellationToken).AsTask());
        }

        [Fact]
        public async Task Should_ThrowDataIntegrityException_When_NonSeekableStreamHasInvalidHeader() {
            // Arrange: Provide a non-seekable stream with invalid/missing header
            byte[] invalidHeaderData = [1, 2, 3, 4, 5, 6, 7, 8];
            using MemoryStream sourceMs = new(invalidHeaderData);
            using MemoryStream compressedMs = new();
            using(System.IO.Compression.GZipStream compressor = new(compressedMs, System.IO.Compression.CompressionMode.Compress, leaveOpen: true)) {
                sourceMs.CopyTo(compressor);
            }
            compressedMs.Position = 0;

            System.IO.Compression.GZipStream nonSeekableStream = new(compressedMs, System.IO.Compression.CompressionMode.Decompress);
            Assert.False(nonSeekableStream.CanSeek);

            NonSeekableStreamStorage customStorage = new(nonSeekableStream);
            BloomFilterOptions options = new();
            options.Lifecycle.EnableIntegrityCheck = false; // Even when integrity check is false, cannot rewind non-seekable stream!

            BloomFilterContext context = new(
                Storage: customStorage,
                MemoryStreamPool: new FakeObjectPool<MemoryStream>(() => new MemoryStream()),
                Logger: NullLogger.Instance,
                Options: options,
                TimeProvider: TimeProvider.System,
                ConfigFactory: this._configFactory
            );

            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("non-seekable-filter"), 1_000, 0.01);
            using InMemoryBloomFilter filter = new(config, context);

            // Act & Assert: Must fail fast with DataIntegrityException rather than corrupting offset
            DataIntegrityException ex = await Assert.ThrowsAsync<DataIntegrityException>(() => filter.ReloadAsync(TestContext.Current.CancellationToken).AsTask());
            Assert.Contains("non-seekable", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class NonSeekableStreamStorage(Stream stream) : IBloomFilterStorage {
            public Task<bool> SaveAsync(FilterName filterName, BloomFilterConfiguration config, Stream source, CancellationToken cancellationToken = default) => Task.FromResult(true);
            public ValueTask<(BloomFilterConfiguration? Config, Stream DataStream)?> LoadStreamAsync(FilterName filterName, CancellationToken cancellationToken = default) => ValueTask.FromResult<(BloomFilterConfiguration?, Stream)?>((null, stream));
            public Task DeleteAsync(FilterName filterName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    public sealed class FingerprintValidation : BloomFilterIntegrityTests {
        [Fact]
        public async Task Should_ThrowDataIntegrityException_When_ConfigurationFingerprintMismatches() {
            // Arrange
            FakeBloomFilterStorage storage = new();
            BloomFilterContext context = CreateContext(storage, enableIntegrityCheck: true);

            BloomFilterConfiguration initialConfig = this._configFactory.Create(FilterName.Parse("fingerprint-mismatch"), 2_000, 0.01, hashSeed: 1111);
            using(InMemoryBloomFilter initialFilter = new(initialConfig, context)) {
                initialFilter.Add("valid-data"u8);
                await initialFilter.SaveAsync(TestContext.Current.CancellationToken);
            }

            BloomFilterConfiguration modifiedConfig = this._configFactory.Create(FilterName.Parse("fingerprint-mismatch"), 2_000, 0.01, hashSeed: 9999);

            // Act & Assert
            using InMemoryBloomFilter reloadedFilter = new(modifiedConfig, context);
            DataIntegrityException ex = await Assert.ThrowsAsync<DataIntegrityException>(() => reloadedFilter.ReloadAsync(TestContext.Current.CancellationToken).AsTask());
            Assert.Contains("fingerprint mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class IntegrityCheckToggle : BloomFilterIntegrityTests {
        [Fact]
        public async Task Should_NotThrowDataIntegrityException_When_IntegrityCheckIsDisabled() {
            // Arrange
            FakeBloomFilterStorage storage = new();
            BloomFilterContext context = CreateContext(storage, enableIntegrityCheck: false);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("disabled-integrity"), 1_000, 0.01);

            using(InMemoryBloomFilter filter = new(config, context)) {
                filter.Add("initial-item"u8);
                await filter.SaveAsync(TestContext.Current.CancellationToken);
            }

            (BloomFilterConfiguration? Config, Stream DataStream)? loadResult = await storage.LoadStreamAsync(config.Name.Value, TestContext.Current.CancellationToken);
            Assert.NotNull(loadResult);

            byte[] payload;
            using(MemoryStream ms = new()) {
                await loadResult.Value.DataStream.CopyToAsync(ms, TestContext.Current.CancellationToken);
                payload = ms.ToArray();
            }

            payload[^1] ^= 0xFF; // Flip bits

            using(MemoryStream corruptedStream = new(payload)) {
                await storage.SaveAsync(config.Name.Value, config, corruptedStream, TestContext.Current.CancellationToken);
            }

            // Act
            using InMemoryBloomFilter reloadedFilter = new(config, context);
            await reloadedFilter.ReloadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(reloadedFilter);
        }
    }
}