using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Wiaoj.Serialization.Proxy.Tests.Unit;

// --- Test İçin Kullanılacak Modeller ---
public class HardcoreUser {
    public int Id { get; set; }
    public string Data { get; set; } = string.Empty;
}

public interface IProxyTestService { }
public class ProxyTestService : IProxyTestService { }
public record ProxyTestKey : ISerializerKey;

public sealed class ObjectProxySerializerTests {
    private readonly ObjectProxySerializer<ProxyTestKey> _serializer = new(new());

    #region 1. REFERANS BÜTÜNLÜĞÜ (THE ILLUSION)
    [Fact]
    public void Serialize_ShouldPreserve_OriginalReference_Exactly() {
        // Arrange
        HardcoreUser original = new() { Id = 101, Data = "Wiaoj-Power" };

        // Act
        byte[] proxyBytes = this._serializer.Serialize(original);
        HardcoreUser? recovered = this._serializer.Deserialize<HardcoreUser>(proxyBytes);

        // Assert: Bu test patlarsa Proxy değildir!
        Assert.NotNull(recovered); //
        Assert.Same(original, recovered); // Bellekteki adresi AYNI mı?
        Assert.True(ReferenceEquals(original, recovered));
    }
    #endregion

    #region 2. CONCURRENCY & SHARDING (THE STRESS TEST)
    [Fact]
    public async Task HighConcurrency_ShouldNot_Cause_DataLoss_Or_Corruption() {
        // Arrange: 100 bin nesne, 100 bin farklı thread/task
        const int TotalMessages = 100_000;
        List<HardcoreUser> sourceItems = Enumerable.Range(0, TotalMessages)
            .Select(i => new HardcoreUser { Id = i, Data = $"Payload-{i}" })
            .ToList();

        ConcurrentBag<(int OriginalId, HardcoreUser? Recovered)> results = [];

        // Act: Sharded Registry'ye (ObjectProxyRegistry) aynı anda abanıyoruz
        // ShardCount kadar kilit havuzu burada test ediliyor.
        await Parallel.ForEachAsync(sourceItems, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, async (user, ct) => {
            // Nesneyi dolaba koy (ID al)
            byte[] data = this._serializer.Serialize(user);

            // Rastgele bir gecikme simülasyonu (Transport gecikmesi)
            if(user.Id % 10 == 0) await Task.Yield();

            // Nesneyi dolaptan geri al
            HardcoreUser? recovered = this._serializer.Deserialize<HardcoreUser>(data);
            results.Add((user.Id, recovered));
        });

        // Assert
        Assert.Equal(TotalMessages, results.Count);
        foreach((int originalId, HardcoreUser? recovered) in results) {
            Assert.NotNull(recovered);
            Assert.Equal(originalId, recovered.Id); // Veriler kaymış mı?
        }
    }
    #endregion

    #region 3. MEMORY LEAK & LIFECYCLE (THE SAFETY)
    [Fact]
    public async Task Lease_Should_Allow_Multiple_Reads_But_Clean_Up_After_Expiry() {
        // Arrange
        ProxyTestService service = new();
        byte[] data = this._serializer.Serialize(service);

        // Act 1: Kiralama süresi içinde defalarca okunabilmeli (Fan-out desteği)
        var firstAttempt = this._serializer.Deserialize<ProxyTestService>(data);
        var secondAttempt = this._serializer.Deserialize<ProxyTestService>(data);

        // Assert 1: İkisi de aynı orijinal nesneyi almalı
        Assert.Same(service, firstAttempt);
        Assert.Same(service, secondAttempt);

        // Act 2: Kiralama süresinin dolmasını bekle (Örn: Registry'de 30sn ise testte bekliyoruz)
        // Not: Testlerin hızlı koşması için Registry'de bu süre test ortamında kısaltılabilir.
        await Task.Delay(TimeSpan.FromSeconds(12));

        // Act 3: Süre dolduktan sonra temizlikçi (CleanupTask) çalışmış olmalı
        var finalAttempt = this._serializer.Deserialize<ProxyTestService>(data);

        // Assert 2: Artık gerçekten silinmiş olmalı
        Assert.Null(finalAttempt);
    }
    #endregion

    [Fact]
    public async Task SlidingExpiration_Should_Keep_Object_Alive_On_Touch() {
        // Arrange
        var item = new HardcoreUser { Id = 1 };
        byte[] data = _serializer.Serialize(item);

        // Act: Sürekli "dokunarak" (deserialize ederek) nesneyi hayatta tutuyoruz
        for(int i = 0; i < 5; i++) {
            await Task.Delay(1000); // 1'er saniye bekleyerek oku
            var recovered = _serializer.Deserialize<HardcoreUser>(data);
            Assert.NotNull(recovered); //
        }
    }

    [Fact]
    public async Task Registry_Should_Cleanup_After_Lease_Expires() {
        // Not: Bu testin çalışması için Registry'deki LeaseDuration 30sn ise 
        // biraz uzun sürer. Test için Registry'ye bir 'InternalClear' veya
        // 'ShortLease' eklemek gerekebilir.

        var item = new HardcoreUser { Id = 1 };
        byte[] data = _serializer.Serialize(item);

        // Act: Hiç dokunmadan bekliyoruz (CleanupTask'ın çalışması için)
        // Registry'deki cleanup 10sn'de bir, lease 30sn
        await Task.Delay(TimeSpan.FromSeconds(12));

        var recovered = _serializer.Deserialize<HardcoreUser>(data);

        // Assert: Artık silinmiş olmalı!
        Assert.Null(recovered);
    }

    #region 4. POLYMORPHISM & INTERFACES (THE FLEXIBILITY)
    [Fact]
    public void Serialization_ShouldHandle_Interfaces_Without_TypeLoss() {
        // Arrange
        IProxyTestService original = new ProxyTestService();

        // Act
        byte[] data = this._serializer.Serialize(original);
        IProxyTestService? recovered = this._serializer.Deserialize<IProxyTestService>(data);

        // Assert
        Assert.NotNull(recovered);
        Assert.IsType<ProxyTestService>(recovered); // Orijinal tip korunmuş mu?
        Assert.Same(original, recovered);
    }
    #endregion

    #region 5. EDGE CASES (THE ROBUSTNESS)
    [Fact]
    public void TryDeserialize_ShouldHandle_InvalidIDs_Gracefully() {
        // Arrange: Registry'de olmayan rastgele bir ID (8 byte)
        byte[] invalidData = new byte[8];
        new Random().NextBytes(invalidData);

        // Act
        bool success = this._serializer.TryDeserialize<HardcoreUser>(invalidData, out HardcoreUser? result);

        // Assert
        Assert.False(success); 
        Assert.Null(result);
    }

    [Fact]
    public void StringProxy_ShouldWork_For_InProcess_Messaging() {
        // Arrange
        HardcoreUser item = new() { Id = 777 };

        // Act
        string proxyId = this._serializer.SerializeToString(item);
        HardcoreUser? recovered = this._serializer.DeserializeFromString<HardcoreUser>(proxyId);

        // Assert
        Assert.Same(item, recovered);
        Assert.True(long.TryParse(proxyId, out _)); // ID'nin bir sayı olduğunu teyit et
    }

    [Fact]
    public async Task StreamAsync_ShouldNot_Block_Or_Copy() {
        // Arrange
        HardcoreUser item = new() { Id = 42 };
        using MemoryStream ms = new();

        // Act
        await this._serializer.SerializeAsync(ms, item);
        ms.Position = 0;
        HardcoreUser? recovered = await this._serializer.DeserializeAsync<HardcoreUser>(ms);

        // Assert
        Assert.Same(item, recovered);
    }
    #endregion

    [Fact]
    public async Task Million_Message_Hammer_Test() {
        // Arrange: 1 Milyon nesne!
        const int Total = 1_000_000;
        var dataSet = Enumerable.Range(0, Total).Select(i => new HardcoreUser { Id = i }).ToArray();
        var proxyStorage = new byte[Total][];

        // Act 1: Paralel Yazma (Hız testi)
        Parallel.For(0, Total, i => {
            proxyStorage[i] = _serializer.Serialize(dataSet[i]);
        });

        // Act 2: Paralel Okuma (Tutarlılık testi)
        var results = new HardcoreUser?[Total];
        Parallel.For(0, Total, i => {
            results[i] = _serializer.Deserialize<HardcoreUser>(proxyStorage[i]);
        });

        // Assert
        Assert.All(results, Assert.NotNull);
        Assert.Equal(Total, results.DistinctBy(u => u!.Id).Count());
    }

    [Fact]
    public async Task Stream_Multiplexing_Should_Maintain_Alignment() {
        // Arrange: Aynı stream'e 100 nesne ID'si yazıyoruz
        using var stream = new MemoryStream();
        var originals = Enumerable.Range(0, 100).Select(i => new HardcoreUser { Id = i }).ToList();

        // Act: Yazma
        foreach(var item in originals) {
            await _serializer.SerializeAsync(stream, item);
        }

        // Başa dön
        stream.Position = 0;

        // Act: Okuma
        foreach(var original in originals) {
            var recovered = await _serializer.DeserializeAsync<HardcoreUser>(stream);
            Assert.Same(original, recovered); // Hizalama bozulursa burada patlar!
        }
    }

    public class Node { public Node? Next { get; set; } }

    [Fact]
    public void CircularReference_Should_Work_Perfectly() {
        // Arrange: Kendine referans veren bir yapı
        var node = new Node();
        node.Next = node;

        // Act: JSON bunu serileştiremez ama Proxy sadece adres taşır!
        byte[] data = _serializer.Serialize(node);
        var recovered = _serializer.Deserialize<Node>(data);

        // Assert
        Assert.Same(node, recovered);
        Assert.Same(recovered, recovered!.Next);
    }

    [Fact]
    public async Task Object_Should_Be_Eligible_For_GC_Only_After_Lease_Expires() {
        // Arrange: Zayıf referans oluştur
        var weakRef = CreateWeakReferenceWithLease();

        // Act 1: Hemen GC çağır (Nesne hala Registry'de olduğu için silinmemeli)
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Assert.True(weakRef.IsAlive, "Nesne kiralama süresi dolmadan silindi! Registry referansı tutamıyor.");

        // Act 2: Kiralama süresinin (Lease) ve temizlik periyodunun dolmasını bekle
        await Task.Delay(TimeSpan.FromSeconds(12));

        // Act 3: Şimdi GC'yi zorla
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Assert: Artık Registry'de Strong Reference kalmadığı için GC nesneyi toplamış olmalı
        Assert.False(weakRef.IsAlive, "Lease dolmasına rağmen nesne hala bellekte!");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference CreateWeakReferenceWithLease() {
        var item = new ProxyTestService();
        var data = this._serializer.Serialize(item);
        // Deserialize yaparak süreyi başlattık (veya sadece Serialize yettli)
        this._serializer.Deserialize<ProxyTestService>(data);
        return new WeakReference(item);
    }

    [Fact]
    public void Mixed_Types_In_Registry_Should_Not_Interfere() {
        // Arrange
        var user = new HardcoreUser { Id = 1 };
        var service = new ProxyTestService();

        // Act
        var userData = _serializer.Serialize(user);
        var serviceData = _serializer.Serialize(service);

        // Assert: Yanlış tiple çekmeye çalışırsak (Invalid Cast) null dönmeli
        Assert.Null(_serializer.Deserialize<ProxyTestService>(userData));
        Assert.Same(service, _serializer.Deserialize<ProxyTestService>(serviceData));
    }
}