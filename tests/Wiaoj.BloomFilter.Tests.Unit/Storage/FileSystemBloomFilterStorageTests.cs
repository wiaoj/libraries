using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Engine;

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

    private FileSystemBloomFilterStorage CreateStorage(bool enableCompression = false) {
        BloomFilterOptions options = new();
        options.Storage.Path = this._tempDirectory;
        options.Storage.EnableCompression = enableCompression;
        options.Storage.IgnoreErrors = false;

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
    }
}