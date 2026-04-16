using System.Diagnostics;

namespace Wiaoj.Serialization.Proxy.Tests.Unit;

public sealed class ObjectProxy2Tests {
    private readonly ObjectProxySerializer<ProxyTestKey> _serializer = new(new());

    #region 1. THE DOUBLE-CLAW RACE (Açgözlü Okuma Yarışı)
    [Fact]
    public async Task Double_Deserialize_Should_Both_Succeed_For_FanOut() {
        // Arrange
        var item = new HardcoreUser { Id = 1 };
        byte[] data = _serializer.Serialize(item);

        // Act: İki farklı thread aynı anda saldırıyor
        var task1 = Task.Run(() => _serializer.Deserialize<HardcoreUser>(data));
        var task2 = Task.Run(() => _serializer.Deserialize<HardcoreUser>(data));

        var results = await Task.WhenAll(task1, task2);

        // Assert: Artık İKİSİ DE nesneyi başarıyla almalı (Aynı referans!)
        Assert.NotNull(results[0]);
        Assert.NotNull(results[1]);
        Assert.Same(results[0], results[1]);
    }
    #endregion

    #region 2. THE GIGANTIC SHIELD (Devasa Nesne Testi)
    [Fact]
    public void Proxying_Gigantic_Array_Should_Be_Instant() {
        // WARMUP (ISINMA): JIT Derleyicisini çalıştırıp aradan çıkarıyoruz.
        _serializer.Serialize(new byte[1]);

        // Arrange: 1 GB'lık devasa dizi
        byte[] hugeArray = new byte[1024 * 1024 * 1024];

        // Act: Gerçek Test
        Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        byte[] proxyId = this._serializer.Serialize(hugeArray);
        watch.Stop();

        // Assert: Artık 1ms'nin bile altında sürecektir!
        Assert.True(watch.ElapsedMilliseconds < 10, $"Proxy çok yavaş: {watch.ElapsedMilliseconds}ms");

        var recovered = this._serializer.Deserialize<byte[]>(proxyId);
        Assert.Same(hugeArray, recovered);
    }
    #endregion

    #region 3. THE BOXING TRAP (Struct Paketleme Testi)
    [Fact]
    public void Proxying_ValueTypes_Should_Work_Via_Boxing() {
        // Proxy aslında class'lar için ama ya biri int veya struct gönderirse?
        int magicNumber = 424242;

        byte[] data = this._serializer.Serialize(magicNumber);
        var recovered = this._serializer.Deserialize<int>(data);

        // Assert: Struct'lar kutulanıp (boxing) taşınabilmeli
        Assert.Equal(magicNumber, recovered);
    }
    #endregion

    #region 4. THE CORRUPT STREAM (Eksik Byte Felaketi)
    [Fact]
    public async Task Deserialize_From_Truncated_Stream_Should_Fail_Safely() {
        // Arrange: 8 byte beklenen yere sadece 4 byte gönderiyoruz
        using MemoryStream ms = new(new byte[] { 1, 2, 3, 4 });

        // Act & Assert: Patlamamalı, uygun hatayı fırlatmalı veya default dönmeli
        await Assert.ThrowsAsync<EndOfStreamException>(async () => {
            await this._serializer.DeserializeAsync<HardcoreUser>(ms);
        });
    }
    #endregion

    #region 5. THE IDENTITY THIEF (Tip Sahtekarlığı)
    [Fact]
    public void Attempt_To_Deserialize_As_Wrong_Interface_Should_Return_Null() {
        // Arrange: Bir Liste kaydediyoruz
        List<string> list = new() { "A", "B" };
        byte[] data = this._serializer.Serialize(list);

        // Act: Onu bir Sözlük (Dictionary) gibi geri okumaya çalışıyoruz
        var recovered = this._serializer.Deserialize<Dictionary<string, string>>(data);
         
        Assert.Null(recovered);
    }
    #endregion

    #region 6. THE MULTI-INSTANCE SYNERGY (Farklı Örnekler Aynı Registry)
    [Fact]
    public void Different_Serializer_Instances_Should_Share_Same_Static_Registry() {
        using var sharedRegistry = new ObjectProxyRegistry();
        // Farklı generic key'lere sahip iki serializer
        ObjectProxySerializer<ProxyTestKey> serializerA = new(sharedRegistry);
        ObjectProxySerializer<KeylessRegistration> serializerB = new(sharedRegistry);

        HardcoreUser item = new() { Id = 55 };

        // Act: A ile kaydet, B ile oku
        byte[] data = serializerA.Serialize(item);
        var recovered = serializerB.Deserialize<HardcoreUser>(data);

        Assert.NotNull(recovered);
        Assert.Same(item, recovered);
    }
    #endregion
}