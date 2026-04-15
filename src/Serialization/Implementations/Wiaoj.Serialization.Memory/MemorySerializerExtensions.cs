using System.Runtime.CompilerServices;
using Wiaoj.Serialization.Memory;

#pragma warning disable IDE0130
namespace Wiaoj.Serialization.DependencyInjection;
#pragma warning restore IDE0130

public static class MemorySerializerExtensions {
    public static ISerializerConfigurator<TKey> UseMemorySerializer<TKey>(this IWiaojSerializationBuilder builder)
        where TKey : ISerializerKey {
        return builder.AddSerializer(sp => new MemorySerializer<TKey>());
    }

    // Sadece unmanaged tipler için özelleşmiş kayıt metodu
    public static ISerializerConfigurator<TKey> UseMemorySerializer<TKey, TMessage>(this IWiaojSerializationBuilder builder)
        where TKey : ISerializerKey
        where TMessage : unmanaged {
        // Boot-time kontrolü: Uygulama ayağa kalkarken burada patlar, kullanıcı hatasını anlar
        MemorySerializationValidator.Validate<TMessage>();

        return builder.AddSerializer(sp => new MemorySerializer<TKey>());
    }
}
public static class MemorySerializationValidator {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate<T>() {
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            throw new InvalidOperationException(
                $"MemorySerializer is strictly limited to blittable/unmanaged structs. " +
                $"Type '{typeof(T).Name}' contains references or is not unmanaged.");
    }
}