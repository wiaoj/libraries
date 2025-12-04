using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Wiaoj.Primitives.Enums;
/// <summary>
/// "Akıllı Enum" (Smart Enum) deseni için nihai, jenerik, tip güvenli ve yüksek performanslı bir temel sınıf.
/// Bu sınıf, standart enum'ların tüm temel özelliklerini (değer bazlı eşitlik, karşılaştırma, Flags/bitwise operasyonlar,
/// öznitelik okuma) desteklerken, onlara davranış (metot) ekleme ve zengin veri tipleriyle çalışma imkanı sunar.
/// </summary>
/// <typeparam name="TEnum">Kalıtım alan sınıfın kendisi (Curiously Recurring Template Pattern).</typeparam>
/// <remarks>
/// <para>
/// Özellikler:
/// <list type="bullet">
/// <item><description><b>Performans:</b> Tüm arama işlemleri (FromValue, FromName) O(1) karmaşıklığındadır.</description></item>
/// <item><description><b>Güvenlik:</b> Başlangıçta yinelenen 'Value' veya 'Name' değerlerini denetler.</description></item>
/// <item><description><b>Flags Desteği:</b> Bitwise operatörleri ('|', '&amp;', '^') ve 'HasFlag' metodunu destekler.</description></item>
/// <item><description><b>Öznitelik (Attribute) Desteği:</b> 'GetAttribute&lt;T&gt;' ve 'Description' gibi yardımcılarla üye özniteliklerine kolay erişim sağlar.</description></item>
/// <item><description><b>Sezgisel Kullanım:</b> Eşitlik (==, !=) ve karşılaştırma (&lt;, &gt;, &lt;=, &gt;=) operatörlerini tam destekler.</description></item>
/// <item><description><b>Sıfır Bağımlılık:</b> Sadece .NET temel sınıf kütüphanelerini kullanır.</description></item>
/// </list>
/// </para>
/// </remarks>
public abstract record class Enumeration<TEnum> : IEquatable<TEnum>, IComparable<TEnum> where TEnum : Enumeration<TEnum> {
    private static readonly IReadOnlyCollection<TEnum> _allInstances;
    private static readonly Dictionary<int, TEnum> _fromValue;
    private static readonly Dictionary<string, TEnum> _fromName;
    private static readonly Dictionary<TEnum, FieldInfo> _fromInstanceToField;

    /// <summary>
    /// Enum'ın sayısal karşılığı. Eşitlik, karşılaştırma ve sıralama işlemleri bu değer üzerinden yapılır.
    /// </summary>
    public int Value { get; init; }

    /// <summary>
    /// Enum'ın metin karşılığı. Genellikle kullanıcı arayüzünde gösterim için kullanılır.
    /// </summary>
    public string Name { get; init; }

    static Enumeration() {
        Type enumType = typeof(TEnum);
        IEnumerable<FieldInfo> fields = enumType
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.FieldType == enumType);

        List<TEnum> instances = [];
        _fromInstanceToField = [];

        foreach (FieldInfo? field in fields) {
            TEnum instance = (TEnum)field.GetValue(null)!;
            instances.Add(instance);
            _fromInstanceToField.Add(instance, field);
        }

        _allInstances = instances.AsReadOnly();

        try {
            _fromValue = instances.ToDictionary(e => e.Value);
            _fromName = instances.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException ex) {
            throw new TypeInitializationException(enumType.FullName,
                new InvalidOperationException($"'{enumType.Name}' türü başlatılamadı. Lütfen tüm enum üyelerinin benzersiz 'Value' ve 'Name' (büyük/küçük harf duyarsız) değerlerine sahip olduğundan emin olun.", ex));
        }
    }

    protected Enumeration(int value, string name) {
        this.Value = value;
        this.Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public static IReadOnlyCollection<TEnum> GetAll() {
        return _allInstances;
    }

    public static TEnum FromValue(int value) {
        if (_fromValue.TryGetValue(value, out TEnum? result)) {
            return result;
        }
        // Değer bulunamazsa, belki bir bitwise kombinasyonudur.
        return CreateFlagCombination(value);
    }

    public static TEnum FromName(string name) {
        ArgumentNullException.ThrowIfNull(name);
        if (_fromName.TryGetValue(name, out TEnum? result)) {
            return result;
        }
        throw new KeyNotFoundException($"'{typeof(TEnum).Name}' türünde '{name}' ismine sahip bir üye bulunamadı.");
    }

    public static bool TryFromValue(int value, [MaybeNullWhen(false)] out TEnum result) {
        if (_fromValue.TryGetValue(value, out result)) {
            return true;
        }

        result = CreateFlagCombination(value);
        return true; // Flags için her zaman geçerli bir kombinasyon oluşturulabilir.
    }

    public static bool TryFromName(string? name, [MaybeNullWhen(false)] out TEnum result) {
        if (name is null) {
            result = null;
            return false;
        }
        return _fromName.TryGetValue(name, out result);
    }

    #region Attribute Support

    /// <summary>
    /// Bu enum üyesine atanmış olan belirtilen türdeki ilk özniteliği (attribute) döndürür.
    /// </summary>
    /// <typeparam name="TAttribute">Aranacak öznitelik türü.</typeparam>
    /// <returns>Bulunan öznitelik veya bulunamazsa null.</returns>
    public TAttribute? GetAttribute<TAttribute>() where TAttribute : Attribute {
        if (_fromInstanceToField.TryGetValue((TEnum)this, out FieldInfo? fieldInfo)) {
            return fieldInfo.GetCustomAttribute<TAttribute>();
        }
        return null;
    }

    /// <summary>
    /// Bu enum üyesinin [Description] özniteliğini veya öznitelik yoksa 'Name' özelliğini döndürür.
    /// UI gösterimleri için oldukça kullanışlıdır.
    /// </summary>
    public string Description => GetAttribute<DescriptionAttribute>()?.Description ?? this.Name;

    #endregion

    #region Bitwise Operations

    /// <summary>
    /// Bu enum üyesinin, belirtilen bayrakları (flag) içerip içermediğini kontrol eder.
    /// </summary>
    /// <param name="flag">Kontrol edilecek bayrak(lar).</param>
    /// <returns>Bayrakları içeriyorsa true, aksi takdirde false.</returns>
    public bool HasFlag(TEnum flag) {
        if (flag is null) return false;
        // Eğer her iki değer de 0 ise, sadece birbirlerine eşitlerse true döner.
        if (this.Value == 0 || flag.Value == 0) return this.Value == flag.Value;

        return (this.Value & flag.Value) == flag.Value;
    }

    public static TEnum operator |(Enumeration<TEnum> left, Enumeration<TEnum> right) {
        return CreateFlagCombination(left.Value | right.Value);
    }

    public static TEnum operator &(Enumeration<TEnum> left, Enumeration<TEnum> right) {
        return CreateFlagCombination(left.Value & right.Value);
    }

    public static TEnum operator ^(Enumeration<TEnum> left, Enumeration<TEnum> right) {
        return CreateFlagCombination(left.Value ^ right.Value);
    }

    private static TEnum CreateFlagCombination(int value) {
        // Eğer değer zaten tanımlı bir üyeye karşılık geliyorsa, onu döndür.
        if (_fromValue.TryGetValue(value, out TEnum? predefined)) {
            return predefined;
        }

        // Değeri oluşturan tanımlı üyeleri bul ve isimlerini birleştir.
        List<string> matchingFlags = _allInstances
            .Where(e => e.Value != 0 && (value & e.Value) == e.Value)
            .Select(e => e.Name)
            .ToList();

        string name = string.Join(", ", matchingFlags);

        // Activator kullanarak yeni bir 'geçici' enum üyesi oluştur.
        // Bu, TEnum'un private/protected bir (int, string) kurucusuna sahip olmasını gerektirir.
        return (TEnum)Activator.CreateInstance(typeof(TEnum),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            [value, name],
            null)!;
    }

    #endregion

    #region Equality, Comparison, and Conversions

    public virtual bool Equals(TEnum? other) {
        return other is not null && this.Value == other.Value;
    }

    public override int GetHashCode() {
        return this.Value.GetHashCode();
    }

    public int CompareTo(TEnum? other) {
        return other is null ? 1 : this.Value.CompareTo(other.Value);
    }

    public override string ToString() {
        return this.Name;
    }

    public static bool operator <(Enumeration<TEnum> left, Enumeration<TEnum> right) {
        return left.CompareTo((TEnum?)right) < 0;
    }

    public static bool operator <=(Enumeration<TEnum> left, Enumeration<TEnum> right) {
        return left.CompareTo((TEnum?)right) <= 0;
    }

    public static bool operator >(Enumeration<TEnum> left, Enumeration<TEnum> right) {
        return left.CompareTo((TEnum?)right) > 0;
    }

    public static bool operator >=(Enumeration<TEnum> left, Enumeration<TEnum> right) {
        return left.CompareTo((TEnum?)right) >= 0;
    }

    public static implicit operator int(Enumeration<TEnum> @enum) {
        return @enum.Value;
    }

    public static implicit operator string(Enumeration<TEnum> @enum) {
        return @enum.Name;
    }

    public static explicit operator Enumeration<TEnum>(int value) {
        return FromValue(value);
    }

    #endregion
}