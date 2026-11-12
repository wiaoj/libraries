using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Current = Wiaoj.Serialization.SystemTextJsonSerializerExtensions;

namespace Wiaoj.Serialization.DependencyInjection;

/// <summary>
/// Where these methods were before they moved to <see cref="Current"/>, kept so an assembly built against the old
/// namespace — a published Tyto package, say — still binds to them at run time.
/// </summary>
/// <remarks>
/// Plain static methods, not extensions, so source written today sees only <see cref="Current"/>: a caller compiled
/// before the move binds by type, name and signature, none of which an extension's attribute is part of.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete("Moved to Wiaoj.Serialization.SystemTextJsonSerializerExtensions; kept for assemblies built before the move.")]
public static class SystemTextJsonSerializerExtensions {
    public static ISerializerConfigurator<KeylessRegistration> UseSystemTextJson(ISerializationBuilder builder) => Current.UseSystemTextJson(builder);
    public static ISerializerConfigurator<KeylessRegistration> UseSystemTextJson(ISerializationBuilder builder, JsonSerializerOptions options) => Current.UseSystemTextJson(builder, options);
    public static ISerializerConfigurator<KeylessRegistration> UseSystemTextJson(ISerializationBuilder builder, IJsonTypeInfoResolver resolver) => Current.UseSystemTextJson(builder, resolver);
    public static ISerializerConfigurator<KeylessRegistration> UseSystemTextJson(ISerializationBuilder builder, Action<JsonSerializerOptions> configure) => Current.UseSystemTextJson(builder, configure);
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(ISerializationBuilder builder) where TKey : ISerializerKey => Current.UseSystemTextJson<TKey>(builder);
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(ISerializationBuilder builder, JsonSerializerOptions options) where TKey : ISerializerKey => Current.UseSystemTextJson<TKey>(builder, options);
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(ISerializationBuilder builder, IJsonTypeInfoResolver resolver) where TKey : ISerializerKey => Current.UseSystemTextJson<TKey>(builder, resolver);
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey, TContext>(ISerializationBuilder builder) where TKey : ISerializerKey where TContext : JsonSerializerContext, new() => Current.UseSystemTextJson<TKey, TContext>(builder);
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(ISerializationBuilder builder, Action<JsonSerializerOptions> configure) where TKey : ISerializerKey => Current.UseSystemTextJson<TKey>(builder, configure);
    public static ISerializerConfigurator<KeylessRegistration> TryUseSystemTextJson(ISerializationBuilder builder) => Current.TryUseSystemTextJson(builder);
    public static ISerializerConfigurator<KeylessRegistration> TryUseSystemTextJson(ISerializationBuilder builder, JsonSerializerOptions options) => Current.TryUseSystemTextJson(builder, options);
    public static ISerializerConfigurator<KeylessRegistration> TryUseSystemTextJson(ISerializationBuilder builder, IJsonTypeInfoResolver resolver) => Current.TryUseSystemTextJson(builder, resolver);
    public static ISerializerConfigurator<KeylessRegistration> TryUseSystemTextJson(ISerializationBuilder builder, Action<JsonSerializerOptions> configure) => Current.TryUseSystemTextJson(builder, configure);
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey>(ISerializationBuilder builder) where TKey : ISerializerKey => Current.TryUseSystemTextJson<TKey>(builder);
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey>(ISerializationBuilder builder, JsonSerializerOptions options) where TKey : ISerializerKey => Current.TryUseSystemTextJson<TKey>(builder, options);
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey>(ISerializationBuilder builder, IJsonTypeInfoResolver resolver) where TKey : ISerializerKey => Current.TryUseSystemTextJson<TKey>(builder, resolver);
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey, TContext>(ISerializationBuilder builder) where TKey : ISerializerKey where TContext : JsonSerializerContext, new() => Current.TryUseSystemTextJson<TKey, TContext>(builder);
    public static ISerializerConfigurator<TKey> TryUseSystemTextJson<TKey>(ISerializationBuilder builder, Action<JsonSerializerOptions> configure) where TKey : ISerializerKey => Current.TryUseSystemTextJson<TKey>(builder, configure);
    public static ISerializerConfigurator<TKey> UseSystemTextJson<TKey>(ISerializerConfigurator<TKey> configurator, Action<JsonSerializerOptions> configure) where TKey : ISerializerKey => Current.UseSystemTextJson(configurator, configure);
    public static ISerializerConfigurator<TKey> ReplaceSystemTextJson<TKey>(ISerializationBuilder builder, Action<JsonSerializerOptions> configure) where TKey : ISerializerKey => Current.ReplaceSystemTextJson<TKey>(builder, configure);
    public static ISerializerConfigurator<TKey> ReplaceSystemTextJson<TKey>(ISerializerConfigurator<TKey> builder, Action<JsonSerializerOptions> configure) where TKey : ISerializerKey => Current.ReplaceSystemTextJson(builder, configure);
    public static ISerializerConfigurator<TKey> ReplaceSystemTextJson<TKey>(ISerializerConfigurator<TKey> builder, JsonSerializerOptions options) where TKey : ISerializerKey => Current.ReplaceSystemTextJson(builder, options);
}
