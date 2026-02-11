//using System.Security.Cryptography;
//using System.Text;
//using System.Runtime.CompilerServices;

//namespace Wiaoj.Primitives.Obfuscation;

//public enum IdFormat { Base62, Base64Url, Hex }
//public static class Obfuscator {
//    private static uint[]? _keys;
//    private static IdFormat _format = IdFormat.Base62;
//    private static bool _isConfigured;

//    public static void Configure(string seed, IdFormat format = IdFormat.Base62) {
//        _keys = BlockCipher.DeriveKeys(seed); // BlockCipher önceki kodda uint[4] veya [8] dönüyordu, sorun yok.
//        _format = format;
//        _isConfigured = true;
//    }

//    internal static IdFormat CurrentFormat => _format;

//    // 64-bit veriyi 64-bit içinde karıştırır (Boyut korunur)
//    internal static ulong Scramble64(long val) => ScrambleInternal((ulong)val, 0);
//    internal static ulong Descramble64(long val) => DescrambleInternal((ulong)val, 0);

//    // 128-bit veriyi 128-bit içinde karıştırır (Boyut korunur)
//    internal static Int128 Scramble128(Int128 val) {
//        ulong l = (ulong)(val >> 64), r = (ulong)val;
//        return (Int128)ScrambleInternal(l, 2) << 64 | ScrambleInternal(r, 4);
//    }
//    internal static Int128 Descramble128(Int128 val) {
//        ulong l = (ulong)(val >> 64), r = (ulong)val;
//        return (Int128)DescrambleInternal(l, 2) << 64 | DescrambleInternal(r, 4);
//    }

//    private static ulong ScrambleInternal(ulong v, int keyOffset) {
//        uint l = (uint)(v >> 32), r = (uint)v;
//        for(int i = 0; i < 4; i++) {
//            uint t = r;
//            r = l ^ ((r ^ _keys![keyOffset + i]) * 2654435761u);
//            l = t;
//        }
//        return (ulong)l << 32 | r;
//    }

//    private static ulong DescrambleInternal(ulong v, int keyOffset) {
//        uint l = (uint)(v >> 32), r = (uint)v;
//        for(int i = 3; i >= 0; i--) {
//            uint t = l;
//            l = r ^ ((l ^ _keys![keyOffset + i]) * 2654435761u);
//            r = t;
//        }
//        return (ulong)l << 32 | r;
//    }
//}