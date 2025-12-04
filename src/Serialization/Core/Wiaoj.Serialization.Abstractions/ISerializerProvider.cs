using System.Diagnostics.CodeAnalysis;

namespace Wiaoj.Serialization.Abstractions;
/// <summary>
/// Provides access to serializers registered in the DI container.
/// </summary>
public interface ISerializerProvider {
    /// <summary>
    /// Gets the serializer instance for the specified <typeparamref name="TKey"/>.
    /// </summary>
    /// <typeparam name="TKey">
    /// The serializer key type. Must implement <see cref="ISerializerKey"/>.
    /// </typeparam>
    /// <returns>
    /// The serializer for the given <typeparamref name="TKey"/>.
    /// </returns>
    ISerializer<TKey> GetSerializer<TKey>() where TKey : notnull, ISerializerKey;

    /// <summary>
    /// Gets the default <see cref="ISerializer"/> instance.
    /// </summary>
    /// <returns>
    /// The non-generic serializer instance.
    /// </returns>
    ISerializer GetSerializer();

    /// <summary>
    /// Gets the serializer for the given <paramref name="keyType"/>.
    /// </summary>
    /// <param name="keyType">
    /// The type used as a serializer key. Must implement <see cref="ISerializerKey"/>.
    /// </param>
    /// <returns>
    /// The serializer instance if found; otherwise, <see langword="null"/>.
    /// </returns>
    /// <exception cref="PrecaArgumentException">
    /// Thrown when <paramref name="keyType"/> does not implement <see cref="ISerializerKey"/>.
    /// </exception>
    ISerializer? GetSerializer([NotNull] Type keyType);
}

public static class SerializerProviderExtensions {
    public static ISerializer GetRequiredSerializer(this ISerializerProvider provider, [NotNull] Type keyType) {
        Preca.ThrowIfNull(provider);
        Preca.ThrowIfNull(keyType);

        ISerializer? serializer = provider.GetSerializer(keyType);

        Preca.ThrowIfNull(serializer, () => new InvalidOperationException($"Serializer for key '{keyType.Name}' not found."));
        return serializer;
    }
}