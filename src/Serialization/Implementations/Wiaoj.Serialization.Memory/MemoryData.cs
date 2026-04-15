using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wiaoj.Serialization.Memory;

/// <summary>
/// Yüksek performanslı bellek okuma/yazma motoru. Zero-copy işlemler için kullanılır.
/// </summary>
public readonly struct MemoryData {
    private readonly ReadOnlyMemory<byte> _memory;

    public MemoryData(ReadOnlyMemory<byte> memory) => _memory = memory;
    public MemoryData(byte[] array) => _memory = array;

    public int Length => _memory.Length;
    public ReadOnlySpan<byte> Span => _memory.Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureBlittable<T>() {
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            throw new InvalidOperationException($"Type {typeof(T).Name} is not blittable. MemoryData only supports structs without reference types.");
    }

    // --- OLUŞTURMA (YAZMA) ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryData Create<T>(in T value) {
        EnsureBlittable<T>();
        int size = Unsafe.SizeOf<T>();
        byte[] buffer = GC.AllocateUninitializedArray<byte>(size);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetArrayDataReference(buffer), value);
        return new MemoryData(buffer);
    }

    // Allocation yapmadan direkt BufferWriter'a yazma (Zero-Copy)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteTo<T>(IBufferWriter<byte> writer, in T value) {
        EnsureBlittable<T>();
        int size = Unsafe.SizeOf<T>();
        Span<byte> span = writer.GetSpan(size);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(span), value);
        writer.Advance(size);
    }

    // --- OKUMA (ZERO-COPY DESERIALIZATION) ---
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ReadAs<T>() {
        EnsureBlittable<T>();
        if(_memory.Length < Unsafe.SizeOf<T>())
            throw new ArgumentOutOfRangeException(nameof(T), "Buffer is too small.");

        return Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(_memory.Span));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T ReadAsRef<T>() where T : struct {
        EnsureBlittable<T>();
        if(_memory.Length < Unsafe.SizeOf<T>())
            throw new ArgumentOutOfRangeException("Buffer is too small.");

        return ref MemoryMarshal.Cast<byte, T>(_memory.Span)[0];
    }

    // --- DÖNÜŞÜMLER ---
    public override string ToString() => Convert.ToBase64String(_memory.Span);
    public static MemoryData FromString(string data) => new MemoryData(Convert.FromBase64String(data));
    public byte[] ToArray() => _memory.ToArray();

    public static implicit operator ReadOnlySpan<byte>(MemoryData data) => data._memory.Span;
    public static implicit operator ReadOnlyMemory<byte>(MemoryData data) => data._memory; 
    
    public static implicit operator string(MemoryData data) => data.ToString();
    public static explicit operator byte[](MemoryData data) => data.ToArray();
}

/* TODO:@wiaoj
================================================================================
MEMORYDATA MİMARİ ANALİZİ VE İYİLEŞTİRME ÖNERİLERİ
================================================================================

[+] GÜÇLÜ YANLAR (Mevcut haliyle çok iyi):
1. EnsureBlittable<T> kullanımı harika; referans tiplerin memory corruption 
   yaratmasını engelliyor.
2. GC.AllocateUninitializedArray sıfırlama maliyetini atlayarak performansı artırır.
3. IBufferWriter<byte> entegrasyonu zero-copy/allocation-free yazma için en iyi yol.
4. Unsafe.Read/WriteUnaligned kullanımı, struct padding kaynaklı hizalama 
   çökmelerini (özellikle ARM mimarilerinde) güvenle önler.

[!] DÜZELTİLMESİ GEREKEN TEHLİKELER (Refactor Önerileri):
1. String/Base64 Operatörleri (KRİTİK): 
   - ToString() ve implicit operator string KESİNLİKLE kaldırılmalı. 
   - Debugger veya loglama kazara tetiklerse devasa bellek (allocation) tüketir.
   - Çözüm: ToString() sadece boyut dönmeli (Örn: $"MemoryData [{Length} bytes]"). 
   - Base64 için açıkça çağrılan `public string ToBase64String()` metodu yazılmalı.

2. Endianness (Bayt Sıralaması):
   - Unaligned işlemleri mevcut işlemciye (genelde Little-Endian) göre yazar. 
   - Veriyi ağdan Big-Endian kullanan başka bir sisteme yollayacaksan sorun çıkar.
   - Sadece kendi sistemlerin arasında konuşuyorsa dert etmene gerek yok.

3. Versiyonlama (Versioning Kırılganlığı):
   - Kaydedilen struct'ın yapısı (alanları) ileride değişirse, eski kaydedilen 
     binary verileri okurken (ReadAs) program patlar veya veriler kayar.
   - Çözüm: Binary datanın başına versiyon belirten bir byte eklenebilir.

[*] İLERİ SEVİYE TAVSİYE (Gerçek Zero-Copy Okuma):
Şu anki ReadAs<T> veriyi stack'e kopyalar. Struct büyükse ve sadece okuma 
yapacaksan, veriyi hiç kopyalamadan bellek adresini dönen şu metodu ekleyebilirsin:

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public ref readonly T ReadAsRef<T>() where T : struct {
    EnsureBlittable<T>();
    if (_memory.Length < Unsafe.SizeOf<T>())
        throw new ArgumentOutOfRangeException("Buffer is too small.");

    return ref MemoryMarshal.Cast<byte, T>(_memory.Span)[0];
}
================================================================================
*/