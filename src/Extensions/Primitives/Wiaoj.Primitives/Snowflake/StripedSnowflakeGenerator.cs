using System.Diagnostics;
using System.Numerics;

namespace Wiaoj.Primitives.Snowflake;

/// <summary>
/// A high-throughput wrapper for SnowflakeGenerator that reduces contention 
/// by distributing requests across multiple internal generator instances (stripes).
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class StripedSnowflakeGenerator : ISnowflakeGenerator {
    // Kasiyerlerimiz (Şeritler)
    private readonly SnowflakeGenerator[] _stripes;

    // Hızlı mod alma işlemi için maske (örn: 15)
    private readonly int _stripeMask;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripedSnowflakeGenerator"/>.
    /// </summary>
    /// <param name="options">Base options.</param>
    /// <param name="stripeCount">
    /// Number of internal stripes. Must be a power of 2 (e.g., 2, 4, 8, 16, 32).
    /// Default is 16. Higher values reduce contention but consume more NodeId bits.
    /// </param>
    public StripedSnowflakeGenerator(SnowflakeOptions options, int stripeCount = 16) {
        // 1. Kural: Stripe sayısı 2'nin kuvveti olmalı (Bitwise işlem hızı için).
        if(stripeCount <= 0 || (stripeCount & (stripeCount - 1)) != 0) {
            throw new ArgumentException("Stripe count must be a power of 2 (e.g., 2, 4, 8, 16).", nameof(stripeCount));
        }

        this._stripeMask = stripeCount - 1;
        this._stripes = new SnowflakeGenerator[stripeCount];

        // Kaç bitin "Kasa Numarası" için gideceğini hesapla.
        // Örn: 16 stripe = 4 bit (2^4 = 16)
        int stripeBits = System.Numerics.BitOperations.Log2((uint)stripeCount);

        // Kullanıcının Sequence bitleri ile Kasa bitleri toplamı NodeId alanına sığmalı.
        // Bunu SnowflakeGenerator içinde zaten kontrol ediyoruz ama burada NodeId'yi değiştireceğimiz için dikkatli olmalıyız.

        for(int i = 0; i < stripeCount; i++) {
            // BURASI ÇOK ÖNEMLİ:
            // Her kasanın NodeId'si farklı olmalı ki ürettikleri ID'ler çakışmasın.
            // Formül: (AnaNodeId << StripeBits) | KasaIndex
            // Örnek: NodeId=1, Stripe=16 (4 bit). 
            // Kasa 0 ID'si:  0001 0000 (16)
            // Kasa 1 ID'si:  0001 0001 (17)
            // ...
            long stripedNodeId = (options.NodeId << stripeBits) | (long)i;
            if(stripedNodeId > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(options.NodeId),
                    "NodeId and StripeCount combination exceeds 16-bit range.");
            }
            // Yeni options objesi oluştur (Record veya class kopyalama)
            SnowflakeOptions stripeOptions = new() {
                Epoch = options.Epoch,
                SequenceBits = options.SequenceBits,
                MaxDriftMs = options.MaxDriftMs,
                TimeProvider = options.TimeProvider,

                // Kritik nokta: Değiştirilmiş NodeId'yi veriyoruz.
                NodeId = (ushort)stripedNodeId
            };

            // Her şerit için gerçek bir SnowflakeGenerator oluşturup diziye atıyoruz.
            // Not: Senin zaten yazdığın 'SnowflakeGenerator' padding'li olduğu için
            // bu dizideki referanslar birbirinin cache'ini bozmaz.
            this._stripes[i] = new SnowflakeGenerator(stripeOptions);
        }
    }

    /// <summary>
    /// Generates the next unique ID using a thread-local stripe selection.
    /// </summary>
    public SnowflakeId NextId() {
        int index = Environment.CurrentManagedThreadId & this._stripeMask;
        return this._stripes[index].NextId();
    }

    /// <summary>
    /// Extracts the timestamp from a <see cref="SnowflakeId"/> using the internal configuration.
    /// </summary>
    public UnixTimestamp ExtractUnixTimestamp(SnowflakeId id) {
        // Tüm şeritler aynı Epoch/Shift kullandığı için herhangi biriyle decode edilebilir.
        return this._stripes[0].ExtractUnixTimestamp(id);
    }

    /// <summary>
    /// Creates a floor Snowflake ID from a given timestamp (useful for DB range queries).
    /// </summary>
    public SnowflakeId CreateIdFromTimestamp(UnixTimestamp timestamp) {
        return this._stripes[0].CreateIdFromTimestamp(timestamp);
    }

    /// <summary>
    /// Decodes an ID into its human-readable components.
    /// </summary>
    public SnowflakeMetadata Decode(SnowflakeId id) { 
        return this._stripes[0].Decode(id);
    }

    // --- Debugger Helper ---
    private string DebuggerDisplay {
        get {
            int stripeBits = BitOperations.Log2((uint)_stripes.Length);
            long baseNodeId = _stripes[0].NodeId >> stripeBits;
            return $"Striped: Stripes={_stripes.Length}, BaseNodeId={baseNodeId}";
        }
    }
}