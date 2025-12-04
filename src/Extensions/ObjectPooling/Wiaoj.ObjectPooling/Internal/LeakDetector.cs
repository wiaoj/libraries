#if DEBUG
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Wiaoj.ObjectPool.Internal; 
/// <summary>
/// A helper class responsible for tracking leased objects to detect leaks.
/// A leak occurs if an object is garbage-collected without being returned to the pool.
/// This class is only active in DEBUG builds.
/// </summary>
internal static class LeakDetector {
    private static readonly ConditionalWeakTable<object, LeakTracker> TrackedObjects = [];

    /// <summary>
    /// Starts tracking a newly leased object.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Track(object instance) {
        // Skip the top frames related to the pooling library itself to get to the user's code.
        StackTrace stackTrace = new(2, true);
        TrackedObjects.Add(instance, new LeakTracker(instance.GetType().Name, stackTrace));
    }

    /// <summary>
    /// Stops tracking an object that has been successfully returned to the pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Untrack(object instance) {
        if (TrackedObjects.TryGetValue(instance, out LeakTracker? tracker)) {
            tracker.Suppress();
            TrackedObjects.Remove(instance);
        }
    }

    /// <summary>
    /// An associated object that uses its finalizer to report a leak.
    /// </summary>
    private sealed class LeakTracker(string typeName, StackTrace stackTrace) {
        public void Suppress() {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The finalizer. This is only called by the GC if the parent LeakTracker
        /// is being collected, which only happens if the original leased object was
        /// collected without Untrack() being called. This is the leak.
        /// </summary>
        ~LeakTracker() {
            string formattedStackTrace = stackTrace.ToString().Replace("\r\n", "\n").Replace("\n", "\n    ");

            // Log to Debug output. This will appear in Visual Studio's Output window.
            Debug.WriteLine($"""
            -------------------------------------------------
            [WIAOJ.OBJECTPOOLING LEAK DETECTED]
            An object of type '{typeName}' was garbage-collected without being returned to the pool.
            This is a memory leak and indicates that PooledObject<T>.Dispose() was not called.
            Ensure the PooledObject<T> is used within a 'using' statement or its Dispose() method is called explicitly.
            
            Leased at:
                {formattedStackTrace}
            -------------------------------------------------
            """);
        }
    }
}
#endif