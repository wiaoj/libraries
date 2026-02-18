using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wiaoj.Concurrency;

namespace Wiaoj.Primitives.Snowflake;
/// <summary>
/// A high-performance, thread-safe, lock-free ID generator based on the Snowflake algorithm.
/// Optimized with Cache Line Padding to prevent False Sharing.
/// Uses a Monotonic Clock (Stopwatch) to prevent issues with system clock rollback (NTP adjustments).
/// </summary>
[System.Diagnostics.DebuggerDisplay("NodeId = {_nodeId}, SequenceBits = {_sequenceBits}, MaxDrift = {_maxDriftMs}ms")]
[StructLayout(LayoutKind.Explicit)]
public class SnowflakeGenerator : ISnowflakeGenerator {
    // -----------------------------------------------------------------------
    // COLD DATA (Read-Only configuration)
    // Sık erişilen ama değişmeyen verileri başlangıça topluyoruz.
    // -----------------------------------------------------------------------

    // Reference types (GC referansları) genellikle en başta hizalanır.
    [FieldOffset(0)]
    private readonly TimeProvider _timeProvider;

    [FieldOffset(8)]
    private readonly long _nodeId;

    [FieldOffset(16)]
    private readonly long _epochTicks;

    [FieldOffset(24)]
    private readonly long _sequenceMask;

    [FieldOffset(32)]
    private readonly long _maxDriftMs;

    // Monotonic Clock Anchors
    [FieldOffset(40)]
    private readonly long _anchorSystemTimeMs;

    [FieldOffset(48)]
    private readonly long _anchorStopwatchTicks;

    [FieldOffset(56)]
    private readonly double _stopwatchToMsMultiplier;

    [FieldOffset(64)]
    private readonly int _sequenceBits;

    [FieldOffset(68)]
    private readonly int _timestampShift;

    [FieldOffset(72)]
    private readonly int _nodeIdShift;

    [FieldOffset(76)]
    private readonly bool _isSystemTime;

    // -----------------------------------------------------------------------
    // PADDING GAP
    // Offset 77'den 128'e kadar olan kısım boş bırakıldı.
    // Bu, manuel "_p0..._p6" değişkenlerinin yaptığı işi yapar.
    // İşlemci önbellek satırını (Cache Line - genelde 64 byte) kirletmemek için
    // Hot Data'yı en az 128. byte'a (veya 64'ün katına) atıyoruz.
    // -----------------------------------------------------------------------

    // -----------------------------------------------------------------------
    // HOT DATA (Frequently modified)
    // Sadece bu alan Interlocked ile değiştirilir.
    // -----------------------------------------------------------------------
    [FieldOffset(128)]
    private long _currentState;

    /// <summary>
    /// The default <see cref="SnowflakeGenerator"/> instance with NodeId = 0.
    /// </summary>
    public static readonly SnowflakeGenerator Default = new(new SnowflakeOptions { NodeId = 0 });

    public long NodeId => _nodeId;
    public int SequenceBits => _sequenceBits;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnowflakeGenerator"/> class with the specified options.
    /// </summary>
    public SnowflakeGenerator(SnowflakeOptions options) {
        this._sequenceBits = options.SequenceBits;

        int nodeIdBits = 22 - this._sequenceBits;

        this._sequenceMask = -1L ^ (-1L << this._sequenceBits);
        this._nodeIdShift = this._sequenceBits;
        this._timestampShift = this._sequenceBits + nodeIdBits;
        this._epochTicks = options.Epoch.ToUnixTimeMilliseconds();

        // Node ID Validation
        long maxNodeId = -1L ^ (-1L << nodeIdBits);
        if(options.NodeId > maxNodeId || options.NodeId < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.NodeId),
                $"NodeId must be between 0 and {maxNodeId} for sequence size {this._sequenceBits}.");
        }
        this._nodeId = options.NodeId;

        // Epoch Validation
        if(options.Epoch > DateTimeOffset.UtcNow) {
            throw new ArgumentOutOfRangeException(nameof(options.Epoch), "Epoch cannot be in the future.");
        }

        // MaxDrift Validation
        if(options.MaxDriftMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.MaxDriftMs), "MaxDriftMs cannot be negative.");
        }
        this._maxDriftMs = options.MaxDriftMs;

        this._timeProvider = options.TimeProvider ?? TimeProvider.System;
        this._isSystemTime = this._timeProvider == TimeProvider.System;

        // -------------------------------------------------------------------
        // MONOTONIC CLOCK INITIALIZATION
        // -------------------------------------------------------------------
        if(this._isSystemTime) {
            //this._anchorSystemTimeMs = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            this._anchorSystemTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            this._anchorStopwatchTicks = Stopwatch.GetTimestamp();

            // OPTİMİZASYON: Bölme yerine çarpma kullanmak için katsayıyı önceden hesapla.
            // Örn: Frequency 10.000.000 ise, Multiplier = 0.0001 olur.
            this._stopwatchToMsMultiplier = 1000.0 / Stopwatch.Frequency;
        }
        else {
            this._anchorSystemTimeMs = 0;
            this._anchorStopwatchTicks = 0;
            this._stopwatchToMsMultiplier = 0;
        }

        // Initialization: Start 1ms behind to avoid collision on immediate first call
        long currentTimestamp = GetCurrentTimestamp();
        this._currentState = ((currentTimestamp - 1) << this._sequenceBits) | this._sequenceMask;
    }

    /// <summary>
    /// Generates the next unique ID. This method is thread-safe and lock-free.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public SnowflakeId NextId() {
        SpinWait spin = new();

        while(true) {
            long current = Atomic.Read(ref this._currentState);

            long currentTimestamp = current >> this._sequenceBits;
            long currentSequence = current & this._sequenceMask;

            long now = GetCurrentTimestamp();

            // 1. ADIM: "Eğer aynı milisaniyede kalsaydık sequence ne olurdu?" hesapla.
            // Bu işlem her zaman yapılır (Branch yok).
            long nextSequenceCandidate = (currentSequence + 1) & this._sequenceMask;

            // 2. ADIM: Zaman ilerledi mi kontrolü.
            bool isTimeAdvanced = now > currentTimestamp;

            // 3. ADIM: Sequence Seçimi (Conditional Move)
            // Eğer zaman ilerlediyse Sequence = 0, değilse Candidate.
            long nextSequence = isTimeAdvanced ? 0 : nextSequenceCandidate;

            // 4. ADIM: Timestamp Seçimi (Conditional Move)
            // Varsayılan: Eğer zaman ilerlediyse 'now' al.
            // İstisna: Zaman ilerlemedi AMA sequence taştıysa (nextSequenceCandidate == 0),
            // o zaman 'currentTimestamp + 1' yap (Geleceğe taşır).
            // Aksi takdirde 'currentTimestamp' kullan.

            long timestampIfSame = (nextSequenceCandidate == 0) ? currentTimestamp + 1 : currentTimestamp;
            long nextTimestamp = isTimeAdvanced ? now : timestampIfSame;

            // 5. ADIM: Drift (Sapma) Kontrolü
            // Bu 'if' bloğu kalmak zorunda çünkü "Spin" işlemi bir akış kontrolüdür, veri hesaplaması değildir.
            // Ancak bu durum 'Exception' gibi nadir olduğu için Branch Predictor bunu iyi yönetir.
            if(nextTimestamp - now > this._maxDriftMs) {
                spin.SpinOnce();
                continue;
            }

            long nextState = (nextTimestamp << this._sequenceBits) | nextSequence;

            if(Atomic.CompareExchange(ref this._currentState, nextState, current)) {
                long id = ((nextTimestamp - this._epochTicks) << this._timestampShift) |
                          (this._nodeId << this._nodeIdShift) |
                          nextSequence;

                return new SnowflakeId(id);
            }

            spin.SpinOnce();
        }
    }

    /// <summary>
    /// Extracts the timestamp from a <see cref="SnowflakeId"/> using the internal configuration.
    /// </summary>
    public UnixTimestamp ExtractUnixTimestamp(SnowflakeId id) { 
        long timestampDelta = id.Value >> this._timestampShift; 
        return UnixTimestamp.FromMilliseconds(this._epochTicks + timestampDelta);
    }

    /// <summary>
    /// Creates a placeholder Snowflake ID from a given timestamp.
    /// Useful for searching: "Give me all IDs created after 2024-05-01".
    /// </summary>
    public SnowflakeId CreateIdFromTimestamp(UnixTimestamp timestamp) {
        long totalMs = timestamp.TotalMilliseconds;
        long delta = totalMs - this._epochTicks;

        Preca.ThrowIfNegativeOrZero(
            delta,
            () => new ArgumentOutOfRangeException(nameof(timestamp), "Timestamp cannot be earlier than the generator's epoch."));

        long id = delta << this._timestampShift;
        return new SnowflakeId(id);
    }

    /// <summary>
    /// Gets the current timestamp in milliseconds.
    /// Uses Stopwatch (Monotonic Clock) if using System time to prevent issues with clock rollback.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetCurrentTimestamp() {
        if(this._isSystemTime) {
            long elapsedTicks = Stopwatch.GetTimestamp() - this._anchorStopwatchTicks;
            long elapsedMs = (long)(elapsedTicks * this._stopwatchToMsMultiplier);

            return this._anchorSystemTimeMs + elapsedMs;
        }

        return this._timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
    }

    public SnowflakeMetadata Decode(SnowflakeId id) {
        long val = (long)id;

        // Dinamik bit kaydırmalar (Constructor'da hesapladığın shift değerlerini kullan)
        long sequence = val & this._sequenceMask;
        long nodeId = (val >> this._nodeIdShift) & ((-1L ^ (-1L << (this._timestampShift - this._nodeIdShift))));
        long timestampDelta = val >> this._timestampShift;

        var dt = DateTimeOffset.FromUnixTimeMilliseconds(this._epochTicks + timestampDelta);

        return new SnowflakeMetadata(dt, nodeId, sequence);
    }
}
public record struct SnowflakeMetadata(DateTimeOffset Timestamp, long NodeId, long Sequence);
