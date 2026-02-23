using Wiaoj.Concurrency;
using Wiaoj.DistributedCounter.Diagnostics;
using Wiaoj.Primitives;

namespace Wiaoj.DistributedCounter.Internal;

internal sealed class BufferedDistributedCounter : IDistributedCounter {
    private long _expiryTicks = -1;
    public CounterKey Key { get; }
    public CounterStrategy Strategy => CounterStrategy.Buffered;

    private readonly ICounterStorage _storage;

    private readonly AsyncLazy<Empty> _initialSyncTask;

    private long _localDelta;
    private long _baseValue;

    public BufferedDistributedCounter(CounterKey key, ICounterStorage storage) {
        this.Key = key;
        this._storage = storage;

        // Lazy Tanımlama: Sadece ilk çağrıldığında çalışır.
        // Redis'ten mevcut değeri çeker ve _baseValue'ya yazar.
        this._initialSyncTask = new AsyncLazy<Empty>(async cancellationToken => {
            CounterValue remoteValue = await this._storage.GetAsync(this.Key, cancellationToken);

            Atomic.Write(ref this._baseValue, remoteValue.Value);

            return Empty.Default;
        });
    }

    // --- Helper ---
    // Her işlemden önce çağıracağımız metot
    private ValueTask<Empty> EnsureInitializedAsync(CancellationToken cancellationToken) {
        return this._initialSyncTask.GetValueAsync(cancellationToken);
    }

    public async ValueTask<CounterValue> IncrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        // 1. Önce veri güncel mi emin ol
        await EnsureInitializedAsync(cancellationToken);

        if(expiry.Value.HasValue) {
            Atomic.Exchange(ref this._expiryTicks, expiry.Value.Value.Ticks);
        }


        // 2. RAM'de atomik artır
        Atomic.Add(ref this._localDelta, amount);

        // 3. Tahmini değeri dön
        DistributedCounterMetrics.RecordIncrement(this.Key.Value, "Buffered", amount);
        long currentTotal = Atomic.Read(ref this._baseValue) + Atomic.Read(ref this._localDelta);
        return new CounterValue(currentTotal);
    }

    public async ValueTask<CounterLimitResult> TryIncrementAsync(long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken = default) {
        // 1. Önce RAM'dekileri boşalt!
        await FlushAsync(cancellationToken);

        // 2. Redis'e sor
        CounterLimitResult result = await this._storage.TryIncrementAsync(this.Key, amount, limit, expiry, cancellationToken);

        // 3. Başarılıysa Base'i güncelle (Self-healing)
        if(result.IsAllowed) {
            Atomic.Write(ref this._baseValue, result.CurrentValue);
        }

        return result;
    }

    public ValueTask<CounterValue> DecrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        return IncrementAsync(-amount, expiry, cancellationToken);
    }

    public async ValueTask<CounterLimitResult> TryDecrementAsync(long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) {
        await FlushAsync(cancellationToken);

        CounterLimitResult result = await this._storage.TryDecrementAsync(this.Key, amount, minLimit, expiry, cancellationToken);

        if(result.IsAllowed) {
            Atomic.Write(ref this._baseValue, result.CurrentValue);
        }

        return result;
    }

    public async ValueTask<CounterValue> GetValueAsync(CancellationToken cancellationToken) {
        await EnsureInitializedAsync(cancellationToken);

        long val = Atomic.Read(ref this._baseValue) + Atomic.Read(ref this._localDelta);
        return new CounterValue(val);
    }

    public async ValueTask ResetAsync(CancellationToken cancellationToken) {
        // Reset işlemi InitialSync'i beklememeli, aksine onu da sıfırlamalı gibi düşünebilirsin
        // ama basitlik adına akışı bozmuyoruz.
        Atomic.Exchange(ref this._localDelta, 0);
        Atomic.Exchange(ref this._baseValue, 0);
        await this._storage.DeleteAsync(this.Key, cancellationToken);
        // Not: _initialSyncTask bir kere çalıştıysa bir daha çalışmaz.
        // Eğer Reset sonrası tekrar Redis'ten çekmesini istersen AsyncLazy yapısı buna uygun değil.
        // Ancak Reset zaten "0" kabul ettiğimiz için _baseValue=0 ataması yeterli.
    }

    /// <summary>
    /// Servis tarafından çağrılır. Biriken farkı (delta) çeker ve yerel sayacı sıfırlar.
    /// </summary>
    //internal bool TryCaptureDelta(out long delta) {
    //    // Atomik olarak mevcut birikmiş değeri al ve 0'a eşitle.
    //    delta = Atomic.Exchange(ref this._localDelta, 0);
    //    return delta != 0;
    //}
    internal bool TryCaptureDelta(out long delta, out CounterExpiry expiry) {
        delta = Atomic.Exchange(ref this._localDelta, 0);
        long ticks = Atomic.Exchange(ref this._expiryTicks, -1);

        expiry = ticks > 0 ? CounterExpiry.FromTicks(ticks) : default;

        return delta != 0 || ticks > 0; // Delta 0 olsa bile expiry güncellenmek istenmiş olabilir
    }

    /// <summary>
    /// Servis, Redis işlemini başarıyla tamamlarsa bunu çağırır.
    /// Bu sayede _baseValue (Redis'teki değer) güncellenir ve GetValueAsync() doğru çalışmaya devam eder.
    /// </summary>
    internal void CommitDelta(long delta) {
        Atomic.Add(ref this._baseValue, delta);
    }

    /// <summary>
    /// Eğer Redis işlemi patlarsa, çektiğimiz delta'yı geri koymamız gerekir ki veri kaybolmasın.
    /// </summary>
    internal void RollbackDelta(long delta) {
        Atomic.Add(ref this._localDelta, delta);
    }

    /// <summary>
    /// Redis'ten dönen gerçek değer ile yerel değeri senkronize eder.
    /// Geriye "Drift" (Sapma) miktarını döndürür.
    /// </summary>
    internal long SyncWithStorage(long redisRealValue, long justFlushedDelta) {
        // Şu anki yerel taban değerimiz
        long oldBase = Atomic.Read(ref this._baseValue);

        // Beklentimiz: Eski Taban + Gönderdiğimiz Delta
        long expectedValue = oldBase + justFlushedDelta;

        // Gerçekleşen: redisRealValue

        // Tabanı güncelle
        Atomic.Write(ref this._baseValue, redisRealValue);

        // Sapmayı hesapla (Eğer 0 ise her şey yolunda demektir)
        return redisRealValue - expectedValue;
    }

    internal long GetCurrentBaseValue() {
        return Atomic.Read(ref this._baseValue);
    }

    /// <summary>
    /// RAM'de biriken veriyi anında Storage'a gönderir.
    /// TryIncrement/TryDecrement gibi tutarlılık gerektiren işlemlerde
    /// ve AutoFlush servisi tarafından kullanılır.
    /// </summary>
    internal async ValueTask FlushAsync(CancellationToken cancellationToken) {
        // 1. Önce veri güncel mi emin ol
        await EnsureInitializedAsync(cancellationToken);

        // 2. Biriken farkı (delta) VE Expiry bilgisini atomik olarak al ve sıfırla.
        long delta = Atomic.Exchange(ref this._localDelta, 0);
        long ticks = Atomic.Exchange(ref this._expiryTicks, -1); // -1: Expiry yok/değişmedi demek

        // 3. Gönderilecek veri yoksa çık (Expiry değişmiş olsa bile delta 0 ise genelde işlem yapmayız, 
        // ama sırf expiry güncellemek istiyorsan buradaki if'i ona göre düzenleyebilirsin. 
        // Performans için delta 0 ise gitmiyoruz.)
        if(delta == 0) return;

        // Ticks bilgisini CounterExpiry nesnesine çevir
        // Eğer ticks -1 ise 'default' (yani Infinite/Null) gider, Storage buna göre davranır (TTL'i ellemez veya sonsuz yapar).
        CounterExpiry expiryToSend = ticks > 0
            ? CounterExpiry.From(TimeSpan.FromTicks(ticks))
            : default; // Veya CounterExpiry.Infinite

        try {
            // 4. Storage'a gönderirken artık expiryToSend kullanıyoruz!
            CounterValue newValue = await this._storage.AtomicIncrementAsync(
                this.Key,
                delta,
                expiryToSend, // <--- DÜZELTME BURADA
                cancellationToken);

            // 5. Base değeri güncelle
            Atomic.Write(ref this._baseValue, newValue.Value);
        }
        catch {
            // 6. Hata olursa delta'yı geri iade et
            Atomic.Add(ref this._localDelta, delta);

            // Expiry'yi de geri koymak gerekir mi? 
            // Çok kritik değil ama en son expiry kaybolmasın dersek:
            if(ticks > 0) {
                Atomic.Exchange(ref this._expiryTicks, ticks);
            }

            throw;
        }
    }
}