using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Wiaoj.Preconditions.Exceptions;

namespace Wiaoj.Serialization.DependencyInjection.Internal;

internal sealed class SerializerProvider(IServiceProvider sp) : ISerializerProvider {
    private readonly ConcurrentDictionary<Type, ISerializer> _serializerTypes = new();
    public ISerializer<TKey> GetSerializer<TKey>() where TKey : notnull, ISerializerKey {
        return sp.GetRequiredService<ISerializer<TKey>>();
    }

    public ISerializer GetSerializer() {
        return sp.GetRequiredService<ISerializer>();
    }

    public ISerializer GetSerializer([NotNull] Type keyType) {
        return this._serializerTypes.GetOrAdd(keyType, type => {
            Preca.ThrowIfFalse(
                typeof(ISerializerKey).IsAssignableFrom(type),
                () => new PrecaArgumentException($"Type {type} must implement {nameof(ISerializerKey)}", nameof(type)));

            Type serializerType = typeof(ISerializer<>).MakeGenericType(type);
            return (ISerializer)sp.GetRequiredService(serializerType);
        });
    }

    public ISerializer GetRequiredSerializer([NotNull] Type keyType) {
        Preca.ThrowIfNull(keyType);

        ISerializer? serializer = GetSerializer(keyType);

        Preca.ThrowIfNull(serializer, () => new InvalidOperationException($"Serializer for key '{keyType.Name}' not found."));
        return serializer;
    }

    public ISerializer<TKey>? TryGetSerializer<TKey>() where TKey : notnull, ISerializerKey {
        return sp.GetService<ISerializer<TKey>>(); 
    }
}