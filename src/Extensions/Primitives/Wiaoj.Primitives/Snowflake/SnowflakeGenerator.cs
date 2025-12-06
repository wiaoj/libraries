using System.Runtime.CompilerServices;
using Wiaoj.Concurrency; // Senin Atomic kütüphanen

namespace Wiaoj.Primitives.Snowflake;

/// <summary>
/// A high-performance, thread-safe, lock-free ID generator based on the Snowflake algorithm.
/// Optimized with Cache Line Padding to prevent False Sharing.
/// </summary>
public class SnowflakeGenerator {
    // -----------------------------------------------------------------------
    // COLD DATA (Read-Only configuration)
    // -----------------------------------------------------------------------
    private readonly long _nodeId;
    private readonly long _epochTicks;

    private readonly int _sequenceBits;
    private readonly int _timestampShift;
    private readonly int _nodeIdShift;
    private readonly long _sequenceMask;

    // YENİ: Artık const değil, readonly field.
    private readonly long _maxDriftMs;

    private readonly TimeProvider _timeProvider;
    private readonly bool _isSystemTime;

    // -----------------------------------------------------------------------
    // PADDING (Prevent False Sharing)
    // CPU Cache Line genellikle 64 byte'tır. Hot Data (_currentState) ile
    // Cold Data'yı ayırmak için araya tampon koyuyoruz.
    // -----------------------------------------------------------------------
    private readonly long _p0, _p1, _p2, _p3, _p4, _p5, _p6;

    // -----------------------------------------------------------------------
    // HOT DATA (Frequently modified)
    // -----------------------------------------------------------------------
    private long _currentState;

    /// <summary>
    /// The default <see cref="SnowflakeGenerator"/> instance with NodeId = 0.
    /// </summary>
    public static readonly SnowflakeGenerator Default = new(new SnowflakeOptions { NodeId = 0 });

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
        if (options.NodeId > maxNodeId || options.NodeId < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.NodeId),
                $"NodeId must be between 0 and {maxNodeId} for sequence size {this._sequenceBits}.");
        }
        this._nodeId = options.NodeId;

        // Epoch Validation
        if (options.Epoch > DateTimeOffset.UtcNow) {
            throw new ArgumentOutOfRangeException(nameof(options.Epoch), "Epoch cannot be in the future.");
        }

        // MaxDrift Validation
        if (options.MaxDriftMs < 0) {
            throw new ArgumentOutOfRangeException(nameof(options.MaxDriftMs), "MaxDriftMs cannot be negative.");
        }
        this._maxDriftMs = options.MaxDriftMs;

        this._timeProvider = options.TimeProvider ?? TimeProvider.System;
        this._isSystemTime = this._timeProvider == TimeProvider.System;

        // Initialization: Start 1ms behind
        long currentTimestamp = GetCurrentTimestamp();
        this._currentState = ((currentTimestamp - 1) << this._sequenceBits) | this._sequenceMask;
    }

    /// <summary>
    /// Generates the next unique ID. This method is thread-safe and lock-free.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public SnowflakeId NextId() {
        SpinWait spin = new();

        while (true) {
            // Atomic Read (Senin kütüphanen)
            long current = Atomic.Read(ref this._currentState);

            long currentTimestamp = current >> this._sequenceBits;
            long currentSequence = current & this._sequenceMask;

            long now = GetCurrentTimestamp();
            long nextTimestamp;
            long nextSequence;

            // 1. Durum: Sistem saati ilerledi (Normal)
            if (now > currentTimestamp) {
                nextTimestamp = now;
                nextSequence = 0;
            }
            // 2. Durum: Sistem saati aynı veya geride (Burst / Rollback)
            else {
                nextSequence = (currentSequence + 1) & this._sequenceMask;

                if (nextSequence == 0) {
                    // Sequence doldu -> Geleceğe geç
                    nextTimestamp = currentTimestamp + 1;
                }
                else {
                    // Sequence dolmadı -> Aynı sanal zamanda kal
                    nextTimestamp = currentTimestamp;
                }

                // GÜVENLİK: Config'den gelen _maxDriftMs kullanılıyor
                if (nextTimestamp - now > this._maxDriftMs) {
                    spin.SpinOnce();
                    continue;
                }
            }

            long nextState = (nextTimestamp << this._sequenceBits) | nextSequence;

            // Atomic Compare-Exchange (Senin kütüphanen)
            if (Atomic.CompareExchange(ref this._currentState, nextState, current)) {
                long id = ((nextTimestamp - this._epochTicks) << this._timestampShift) |
                          (this._nodeId << this._nodeIdShift) |
                          nextSequence;

                return new SnowflakeId(id);
            }

            spin.SpinOnce();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetCurrentTimestamp() {
        if (this._isSystemTime) {
            return DateTime.UtcNow.Ticks / 10000;
        }
        return this._timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
    }
}