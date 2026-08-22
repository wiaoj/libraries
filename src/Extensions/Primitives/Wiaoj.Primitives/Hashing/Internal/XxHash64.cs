using System.Runtime.CompilerServices;
using static Wiaoj.Primitives.Hashing.Internal.XxHashShared;

namespace Wiaoj.Primitives.Hashing.Internal;

internal static class XxHash64Core
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Avalanche(ulong hash)
    {
        hash ^= hash >> 33;
        hash *= Prime64_2;
        hash ^= hash >> 29;
        hash *= Prime64_3;
        hash ^= hash >> 32;
        return hash;
    }
}

