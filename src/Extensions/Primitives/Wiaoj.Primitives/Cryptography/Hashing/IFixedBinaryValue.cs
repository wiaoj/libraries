using System.Security.Cryptography;

namespace Wiaoj.Primitives;

/// <summary>
/// Sabit boyutlu, byte tabanlı değer tiplerinin (hash'ler vb.) uyduğu sözleşme.
/// </summary>
/// <remarks>
/// Bu interface KASITLI olarak default implementasyon içermez. Struct'lar için
/// default interface member'ları, somut tip üzerinden doğrudan çağrılamaz ve
/// çağrılabilmesi için gereken interface cast'i struct'ı heap'e boxlar.
/// Bunun yerine gerçek mantık <see cref="FixedBinaryValueOps"/> içindeki generic
/// static metotlarda yaşar; bu metotlar "where T : struct, IFixedBinaryValue&lt;T&gt;"
/// constraint'i sayesinde JIT tarafından her T için özelleştirilir (boxing yok).
/// </remarks>
public interface IFixedBinaryValue<TSelf> where TSelf : struct, IFixedBinaryValue<TSelf> {
    /// <summary>Bu değerin byte cinsinden sabit boyutu.</summary>
    static abstract int SizeInBytes { get; }

    /// <summary>Alttaki byte'lara salt-okunur, tahsissiz bir görünüm.</summary>
    ReadOnlySpan<byte> AsSpan();
}

/// <summary>
/// <see cref="IFixedBinaryValue{TSelf}"/> implement eden tipler için paylaşılan,
/// boxing yapmayan eşitlik/karşılaştırma/hash mantığı.
/// </summary>
public static class FixedBinaryValueOps {
    /// <summary>Zamanlama saldırılarına dayanıklı (constant-time) eşitlik karşılaştırması.</summary>
    public static bool Equals<T>(T left, T right) where T : struct, IFixedBinaryValue<T> {
        return CryptographicOperations.FixedTimeEquals(left.AsSpan(), right.AsSpan());
    }

    /// <summary>Byte dizisi bazlı sözlük (lexicographic) sıralama karşılaştırması.</summary>
    public static int CompareTo<T>(T left, T right) where T : struct, IFixedBinaryValue<T> {
        return left.AsSpan().SequenceCompareTo(right.AsSpan());
    }

    /// <summary><see cref="IComparable.CompareTo(object?)"/> için ortak boxed-object mantığı.</summary>
    public static int CompareToObject<T>(T left, object? obj) where T : struct, IFixedBinaryValue<T> {
        if(obj is null) return 1;
        if(obj is T other) return CompareTo(left, other);
        throw new ArgumentException($"Object must be of type {typeof(T).Name}.", nameof(obj));
    }

    /// <summary>Koleksiyonlarda kullanılabilir, kriptografik olmayan hash kodu.</summary>
    public static int GetHashCode<T>(T value) where T : struct, IFixedBinaryValue<T> {
        HashCode hash = new();
        hash.AddBytes(value.AsSpan());
        return hash.ToHashCode();
    }
}