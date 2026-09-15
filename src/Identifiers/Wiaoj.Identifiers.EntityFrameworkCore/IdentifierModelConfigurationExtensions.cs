using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Wiaoj.Identifiers;
using Wiaoj.Identifiers.EntityFrameworkCore;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.EntityFrameworkCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Maps identifiers to <c>bigint</c> columns, in <see cref="DbContext.ConfigureConventions"/>.
/// </summary>
public static class IdentifierModelConfigurationExtensions {
    private static readonly MethodInfo AddIdentifierMethod =
        typeof(IdentifierModelConfigurationExtensions).GetMethod(nameof(AddIdentifier))!;

    /// <summary>
    /// Stores every <typeparamref name="TId"/> property as its Snowflake value, and generates a new identifier for a
    /// primary key of this type when an entity is added without one.
    /// </summary>
    /// <typeparam name="TId">The identifier type.</typeparam>
    /// <param name="configurationBuilder">The model configuration builder.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <example>
    /// <code>
    /// protected override void ConfigureConventions(ModelConfigurationBuilder builder) {
    ///     builder.AddIdentifier&lt;UserId&gt;();
    ///     builder.AddIdentifier&lt;OrderId&gt;();
    /// }
    /// </code>
    /// </example>
    public static ModelConfigurationBuilder AddIdentifier<TId>(this ModelConfigurationBuilder configurationBuilder)
        where TId : struct, IIdentifier<TId> {
        Preca.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<TId>().HaveConversion<IdentifierValueConverter<TId>>();
        configurationBuilder.Conventions.Add(static _ => new IdentifierKeyConvention<TId>());
        return configurationBuilder;
    }

    /// <summary>
    /// Calls <see cref="AddIdentifier{TId}"/> for every identifier type declared in <paramref name="assemblies"/>.
    /// </summary>
    /// <param name="configurationBuilder">The model configuration builder.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>Uses reflection to find the types; call <see cref="AddIdentifier{TId}"/> per type under trimming.</remarks>
    [RequiresUnreferencedCode("Scans assemblies for identifier types with reflection. Call AddIdentifier<TId>() for each type instead.")]
    [RequiresDynamicCode("Closes AddIdentifier<TId>() over each type found at run time. Call AddIdentifier<TId>() for each type instead.")]
    public static ModelConfigurationBuilder AddIdentifiersFromAssemblies(this ModelConfigurationBuilder configurationBuilder, params Assembly[] assemblies) {
        Preca.ThrowIfNull(configurationBuilder);
        Preca.ThrowIfNull(assemblies);

        foreach(Type type in assemblies.SelectMany(assembly => assembly.GetTypes()).Where(IsIdentifier).Distinct()) {
            AddIdentifierMethod.MakeGenericMethod(type).Invoke(null, [configurationBuilder]);
        }

        return configurationBuilder;
    }

    [RequiresUnreferencedCode("Reads implemented interfaces with reflection.")]
    private static bool IsIdentifier(Type type) {
        return type is { IsValueType: true, IsGenericTypeDefinition: false }
               && type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIdentifier<>) && i.GetGenericArguments()[0] == type);
    }
}

/// <summary>
/// Makes a single-property primary key of type <typeparamref name="TId"/> generated on add, by <c>TId.New()</c> — unless
/// the model configures its value generation explicitly or the key is also a foreign key.
/// </summary>
internal sealed class IdentifierKeyConvention<TId> : IModelFinalizingConvention where TId : struct, IIdentifier<TId> {
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context) {
        foreach(IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes()) {
            if(entityType.FindPrimaryKey() is not { Properties: [IConventionProperty property] }
               || property.ClrType != typeof(TId)
               || property.IsForeignKey()
               || property.DeclaringType != entityType) {
                continue;
            }

            if(property.Builder.ValueGenerated(ValueGenerated.OnAdd) is not null) {
                property.Builder.HasValueGenerator(static (_, _) => new IdentifierValueGenerator<TId>());
            }
        }
    }
}
