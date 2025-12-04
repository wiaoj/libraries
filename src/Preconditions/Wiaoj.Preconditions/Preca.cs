global using global::System.Diagnostics;
global using global::System.Diagnostics.CodeAnalysis;
global using global::System.Runtime.CompilerServices;

namespace Wiaoj.Preconditions;
/// <summary>
/// Provides high-performance, static helper methods for validating method preconditions.
/// Throws an exception if a condition is not met.
/// </summary>
[DebuggerStepThrough, DebuggerNonUserCode, StackTraceHidden]
public static partial class Preca {
    /// <summary>
    /// Provides a gateway to user-defined and community-provided extension methods.
    /// This property is lazily-initialized for zero overhead if not used.
    /// </summary>
    public static PrecaExtensions Extensions => PrecaExtensions.Instance;

    internal static class Constants {
        public const int ZeroInt32 = 0;
        public const long ZeroInt64 = 0L;
        public const ulong ZeroUInt64 = 0UL;
    }
}

/// <summary>
/// A singleton marker class for attaching extension methods to. Do not instantiate directly.
/// </summary>
[DebuggerStepThrough, DebuggerNonUserCode, StackTraceHidden]
public sealed class PrecaExtensions {
    internal static readonly PrecaExtensions Instance = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PrecaExtensions() { }
}