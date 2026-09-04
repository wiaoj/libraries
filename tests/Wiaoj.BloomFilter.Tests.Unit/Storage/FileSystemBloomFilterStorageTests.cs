using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Storage;

namespace Wiaoj.BloomFilter.Tests.Unit.Storage;

public class FileSystemBloomFilterStorageTests : IDisposable {
    private readonly string _tempDirectory;
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    public FileSystemBloomFilterStorageTests() {
        this._tempDirectory = Path.Combine(Path.GetTempPath(), "wbf_storage_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._tempDirectory);
    }

    public void Dispose() {
        if(Directory.Exists(this._tempDirectory)) {
            try { Directory.Delete(this._tempDirectory, recursive: true); }
            catch { /* Best effort cleanup */ }
        }

        GC.SuppressFinalize(this);
    }

    private FileSystemBloomFilterStorage CreateStorage(bool enableCompression = false, bool ignoreErrors = false) {
        BloomFilterOptions options = new();
        options.Storage.Path = this._tempDirectory;
        options.Storage.EnableCompression = enableCompression;
        options.Storage.IgnoreErrors = ignoreErrors;

        IOptions<BloomFilterOptions> optionsWrapper = Options.Create(options);
        return new FileSystemBloomFilterStorage(optionsWrapper, NullLogger<FileSystemBloomFilterStorage>.Instance);
    }

    public sealed class SaveAndLoadMethods : FileSystemBloomFilterStorageTests {
        [Fact]
        public async Task Should_SaveAndLoadStream_WithoutCompression() {
            // Arrange
            FileSystemBloomFilterStorage storage = CreateStorage(enableCompression: false);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("uncompressed-filter"), 1_000, 0.01);

            byte[] dummyData = [0x57, 0x42, 0x46, 0x31, 0x01, 0x02, 0x03, 0x04];
            using MemoryStream sourceStream = new(dummyData);

            // Act: Save to file system
            await storage.SaveAsync(config.Name, config, sourceStream, TestContext.Current.CancellationToken);

            // Load back from file system
            (BloomFilterConfiguration? Config, Stream DataStream)? loadResult = await storage.LoadStreamAsync(config.Name, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(loadResult);
            using Stream loadedStream = loadResult.Value.DataStream;
            using MemoryStream ms = new();
            await loadedStream.CopyToAsync(ms, TestContext.Current.CancellationToken);

            Assert.Equal(dummyData, ms.ToArray());
        }

        [Fact]
        public async Task Should_SaveAndLoadStream_WithGZipCompression() {
            // Arrange
            FileSystemBloomFilterStorage storage = CreateStorage(enableCompression: true);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("compressed-filter"), 1_000, 0.01);

            byte[] largeData = new byte[4096];
            Array.Fill(largeData, (byte)0xAB);
            using MemoryStream sourceStream = new(largeData);

            // Act: Save compressed
            await storage.SaveAsync(config.Name, config, sourceStream, TestContext.Current.CancellationToken);

            // Load decompressed
            (BloomFilterConfiguration? Config, Stream DataStream)? loadResult = await storage.LoadStreamAsync(config.Name, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(loadResult);
            using Stream loadedStream = loadResult.Value.DataStream;
            using MemoryStream ms = new();
            await loadedStream.CopyToAsync(ms, TestContext.Current.CancellationToken);

            Assert.Equal(largeData, ms.ToArray());
        }

        [Fact]
        public async Task Should_ReturnNull_When_FileDoesNotExist() {
            // Arrange
            FileSystemBloomFilterStorage storage = CreateStorage();

            // Act
            (BloomFilterConfiguration? Config, Stream DataStream)? loadResult = await storage.LoadStreamAsync("non-existent-filter", TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(loadResult);
        }

        [Fact]
        public async Task Should_ReturnNull_When_FileExistsButIsEmpty() {
            // Arrange: create an empty 0-byte file
            FileSystemBloomFilterStorage storage = CreateStorage();
            string emptyFilePath = Path.Combine(this._tempDirectory, "empty-filter.wbf");
            await File.WriteAllBytesAsync(emptyFilePath, [], TestContext.Current.CancellationToken);

            // Act
            (BloomFilterConfiguration? Config, Stream DataStream)? loadResult = await storage.LoadStreamAsync("empty-filter", TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(loadResult);
        }

        [Fact]
        public async Task Should_ReturnFalseWithoutThrowing_When_IgnoreErrorsIsTrueAndSaveFails() {
            // Arrange: create storage with ignoreErrors: true and pass an unreadable stream
            FileSystemBloomFilterStorage storage = CreateStorage(ignoreErrors: true);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("failing-save"), 1_000, 0.01);

            MemoryStream faultyStream = new();
            faultyStream.Dispose(); // Disposed stream will throw on CopyToAsync

            // Act
            bool result = await storage.SaveAsync(config.Name, config, faultyStream, TestContext.Current.CancellationToken);

            // Assert: must return false instead of throwing
            Assert.False(result);
        }

        [Fact]
        public async Task Should_DeleteFilterFiles_Successfully() {
            // Arrange
            FileSystemBloomFilterStorage storage = CreateStorage();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("delete-test"), 1_000, 0.01);

            using MemoryStream sourceStream = new([0x01, 0x02, 0x03]);
            await storage.SaveAsync(config.Name, config, sourceStream, TestContext.Current.CancellationToken);

            // Act
            await storage.DeleteAsync(config.Name, TestContext.Current.CancellationToken);
            (BloomFilterConfiguration? Config, Stream DataStream)? loadResult = await storage.LoadStreamAsync(config.Name, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(loadResult);
        }

        [Fact]
        public async Task Should_DeleteAllShardFiles_MatchingPrefixPattern() {
            // Arrange
            FileSystemBloomFilterStorage storage = CreateStorage();
            BloomFilterConfiguration config0 = this._configFactory.Create(FilterName.Parse("shard-pattern_s0"), 1_000, 0.01);
            BloomFilterConfiguration config1 = this._configFactory.Create(FilterName.Parse("shard-pattern_s1"), 1_000, 0.01);

            using MemoryStream ms0 = new([0x01]);
            using MemoryStream ms1 = new([0x02]);
            await storage.SaveAsync(config0.Name, config0, ms0, TestContext.Current.CancellationToken);
            await storage.SaveAsync(config1.Name, config1, ms1, TestContext.Current.CancellationToken);

            // Act: delete using base filter name
            await storage.DeleteAsync(FilterName.Parse("shard-pattern"), TestContext.Current.CancellationToken);

            // Assert: both shard files should be deleted
            Assert.Null(await storage.LoadStreamAsync(config0.Name, TestContext.Current.CancellationToken));
            Assert.Null(await storage.LoadStreamAsync(config1.Name, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_ThrowArgumentException_When_FilterNameIsDefault() {
            FileSystemBloomFilterStorage storage = CreateStorage();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("valid"), 1_000, 0.01);
            using MemoryStream ms = new([0x01]);

            await Assert.ThrowsAnyAsync<ArgumentException>(() => storage.SaveAsync(default, config, ms, TestContext.Current.CancellationToken));
            await Assert.ThrowsAnyAsync<ArgumentException>(() => storage.LoadStreamAsync(default, TestContext.Current.CancellationToken).AsTask());
            await Assert.ThrowsAnyAsync<ArgumentException>(() => storage.DeleteAsync(default, TestContext.Current.CancellationToken));
        }
    }
}