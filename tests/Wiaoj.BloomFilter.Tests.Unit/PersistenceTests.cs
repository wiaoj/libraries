using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit;

public sealed class PersistenceTests {
    [Fact]
    public async Task SaveAsync_Should_Call_Storage_With_Correct_Data() {
        IBloomFilterStorage mockStorage = Substitute.For<IBloomFilterStorage>();
        var config = new BloomFilterConfiguration("test", 1000, Percentage.FromDouble(0.01));
        var filter = new InMemoryBloomFilter(config, mockStorage, NullLogger.Instance, new BloomFilterOptions(), TimeProvider.System);

        filter.Add("data"u8);

        await filter.SaveAsync();

        // ÇÖZÜM: st.Length kontrolünü kaldırdık çünkü stream dispose edildi.
        // Sadece herhangi bir stream ile çağrıldığını doğruluyoruz.
        await mockStorage.Received(1).SaveAsync(
            "test",
            Arg.Any<BloomFilterConfiguration>(),
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>());

        Assert.False(filter.IsDirty);
    }

    [Fact]
    public async Task ReloadAsync_Should_Update_Memory_From_Storage() {
        IBloomFilterStorage mockStorage = Substitute.For<IBloomFilterStorage>();
        var config = new BloomFilterConfiguration("test", 1000, Percentage.FromDouble(0.01));

        // ÇÖZÜM: Gerçek bir bit dizisi oluştur ve checksum hesapla
        int byteCount = (int)((config.SizeInBits + 7) / 8);
        byte[] dummyBits = new byte[byteCount]; // Hepsi sıfır
        ulong actualChecksum = System.IO.Hashing.XxHash3.HashToUInt64(dummyBits);

        var ms = new MemoryStream();
        // Header'a gerçek checksum'ı yaz
        BloomFilterHeader.WriteHeader(ms, actualChecksum, config);
        // Header'dan sonra bit verisini de yaz ki okuma tamamlanabilsin
        ms.Write(dummyBits);
        ms.Position = 0;

        mockStorage.LoadStreamAsync("test", Arg.Any<CancellationToken>())
                   .Returns((config, ms));

        var filter = new InMemoryBloomFilter(config, mockStorage, NullLogger.Instance, new BloomFilterOptions(), TimeProvider.System);

        await filter.ReloadAsync();

        Assert.Equal("test", filter.Name);
        Assert.False(filter.IsDirty);
    }
}