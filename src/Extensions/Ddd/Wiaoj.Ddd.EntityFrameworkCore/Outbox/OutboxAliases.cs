using Wiaoj.Preconditions;
using System.Collections.Concurrent;
using System.Reflection;

namespace Wiaoj.Ddd.EntityFrameworkCore.Outbox;

/// <summary>
/// Assigns a stable logical name to a domain event type for use in the outbox.
/// </summary>
/// <remarks>
/// Persisted rows outlive refactors. Without an alias the outbox has to fall back on the CLR type name, so
/// moving an event to another namespace or assembly makes every queued row referencing it unresolvable.
/// Give events an alias and version it — <c>orders.created.v1</c> — rather than renaming the type and hoping.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class DomainEventAliasAttribute(string alias) : Attribute {
    /// <summary>Gets the stable logical name of the annotated event type.</summary>
    public string Alias { get; } = ValidateAlias(alias);

    private static string ValidateAlias(string alias) {
        Preca.ThrowIfNullOrWhiteSpace(alias);
        return alias;
    }
}

/// <summary>
/// Assigns a stable logical name to a post-commit domain event handler.
/// </summary>
/// <remarks>
/// An outbox row names the handler it is destined for, so the handler's identity is persisted just as the
/// event's is, and carries the same risk on rename. See <see cref="DomainEventAliasAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DomainEventHandlerAliasAttribute(string alias) : Attribute {
    /// <summary>Gets the stable logical name of the annotated handler.</summary>
    public string Alias { get; } = ValidateAlias(alias);

    private static string ValidateAlias(string alias) {
        Preca.ThrowIfNullOrWhiteSpace(alias);
        return alias;
    }
}

/// <summary>
/// Resolves the stable logical names the outbox persists, and maps them back to CLR types.
/// </summary>
public interface IOutboxAliasRegistry {
    /// <summary>Gets the alias persisted for <paramref name="eventType"/>.</summary>
    string GetEventAlias(Type eventType);

    /// <summary>Gets the alias persisted for <paramref name="handlerType"/>.</summary>
    string GetHandlerAlias(Type handlerType);

    /// <summary>
    /// Resolves the event type an alias refers to, or <see langword="null"/> when nothing registered claims
    /// it — a row whose event type has been deleted or renamed without an alias.
    /// </summary>
    Type? ResolveEventType(string alias);

    /// <summary>Registers an event type so its alias can be resolved on the way back.</summary>
    void Register(Type eventType);
}

/// <summary>
/// Default registry. Aliases come from <see cref="DomainEventAliasAttribute"/> /
/// <see cref="DomainEventHandlerAliasAttribute"/> when present, and otherwise from the type's full name.
/// </summary>
/// <remarks>
/// The fallback is <see cref="Type.FullName"/> rather than <see cref="Type.AssemblyQualifiedName"/> on
/// purpose: it survives assembly renames and every version bump, which the assembly-qualified form does not.
/// It still breaks on a namespace move, which is exactly what the attribute exists to prevent.
/// </remarks>
internal sealed class OutboxAliasRegistry : IOutboxAliasRegistry {
    private readonly ConcurrentDictionary<Type, string> _eventAliases = new();
    private readonly ConcurrentDictionary<Type, string> _handlerAliases = new();
    private readonly ConcurrentDictionary<string, Type> _typesByAlias = new(StringComparer.Ordinal);

    public string GetEventAlias(Type eventType) {
        Preca.ThrowIfNull(eventType);

        return this._eventAliases.GetOrAdd(eventType, static type =>
            type.GetCustomAttribute<DomainEventAliasAttribute>()?.Alias ?? type.FullName!);
    }

    public string GetHandlerAlias(Type handlerType) {
        Preca.ThrowIfNull(handlerType);

        return this._handlerAliases.GetOrAdd(handlerType, static type =>
            type.GetCustomAttribute<DomainEventHandlerAliasAttribute>()?.Alias ?? type.FullName!);
    }

    public Type? ResolveEventType(string alias) {
        Preca.ThrowIfNullOrWhiteSpace(alias);

        if(this._typesByAlias.TryGetValue(alias, out Type? known)) {
            return known;
        }

        // Not seen in this process yet — a row written before a restart, or by another instance. Fall back to
        // the CLR name, which is what an unannotated type's alias is.
        Type? resolved = Type.GetType(alias, throwOnError: false);

        if(resolved is not null) {
            Register(resolved);
        }

        return resolved;
    }

    public void Register(Type eventType) {
        Preca.ThrowIfNull(eventType);
        this._typesByAlias[GetEventAlias(eventType)] = eventType;
    }
}
