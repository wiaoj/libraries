using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Wiaoj.Preconditions;
using Wiaoj.Querying.Parsers;

#pragma warning disable IDE0130
namespace Wiaoj.Querying;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring schemas, options, and payload parsers on <see cref="IQueryingBuilder"/>.
/// </summary>
public static class QueryingBuilderExtensions {
    /// <summary>
    /// Configures global <see cref="QueryOptions"/> on the query engine builder.
    /// </summary>
    /// <param name="builder">The query engine builder.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IQueryingBuilder Configure(
        this IQueryingBuilder builder,
        Action<QueryOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        return builder;
    }

    /// <summary>
    /// Configures one or more parameter names to be ignored during URL query string binding.
    /// </summary>
    /// <param name="builder">The query engine builder.</param>
    /// <param name="parameters">The parameter names to ignore.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IQueryingBuilder IgnoreParameters(
        this IQueryingBuilder builder,
        params ReadOnlySpan<string> parameters) {
        Preca.ThrowIfNull(builder);

        if(parameters.IsEmpty) {
            return builder;
        }

        string[] copy = parameters.ToArray();
        builder.Services.Configure<QueryOptions>(options => {
            for(int i = 0; i < copy.Length; i++) {
                string? param = copy[i];
                if(!string.IsNullOrWhiteSpace(param)) {
                    options.IgnoredParameters.Add(param.Trim());
                }
            }
        });

        return builder;
    }

    /// <summary>
    /// Configures parameter names to be ignored during URL query string binding.
    /// </summary>
    /// <param name="builder">The query engine builder.</param>
    /// <param name="parameters">The collection of parameter names to ignore.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IQueryingBuilder IgnoreParameters(
        this IQueryingBuilder builder,
        IEnumerable<string> parameters) {
        Preca.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(parameters);

        string[] copy = parameters as string[] ?? [.. parameters];
        builder.Services.Configure<QueryOptions>(options => {
            for(int i = 0; i < copy.Length; i++) {
                string? param = copy[i];
                if(!string.IsNullOrWhiteSpace(param)) {
                    options.IgnoredParameters.Add(param.Trim());
                }
            }
        });

        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="QuerySchema{TEntity}"/> class as a singleton.
    /// </summary>
    public static IQueryingBuilder AddSchema<
        TEntity,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSchema>(
        this IQueryingBuilder builder)
        where TSchema : QuerySchema<TEntity> {
        Preca.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<TSchema>();
        builder.Services.TryAddSingleton<QuerySchema<TEntity>>(static sp => sp.GetRequiredService<TSchema>());
        return builder;
    }

    /// <summary>
    /// Registers an inline configured <see cref="QuerySchema{TEntity}"/> as a singleton.
    /// </summary>
    public static IQueryingBuilder AddSchema<TEntity>(
        this IQueryingBuilder builder,
        Action<QuerySchema<TEntity>> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        QuerySchema<TEntity> schema = new();
        configure(schema);
        builder.Services.TryAddSingleton(schema);
        return builder;
    }

    /// <summary>
    /// Registers an existing <see cref="QuerySchema{TEntity}"/> instance as a singleton.
    /// </summary>
    public static IQueryingBuilder AddSchema<TEntity>(
        this IQueryingBuilder builder,
        QuerySchema<TEntity> schema) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(schema);

        builder.Services.TryAddSingleton(schema);
        return builder;
    }

    /// <summary>
    /// Registers a custom <see cref="IQueryPayloadParser"/> implementation as a singleton.
    /// </summary>
    public static IQueryingBuilder AddPayloadParser<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TParser>(
        this IQueryingBuilder builder)
        where TParser : class, IQueryPayloadParser {
        Preca.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IQueryPayloadParser, TParser>());
        return builder;
    }

    /// <summary>
    /// Registers an existing <see cref="IQueryPayloadParser"/> instance as a singleton.
    /// </summary>
    public static IQueryingBuilder AddPayloadParser(
        this IQueryingBuilder builder,
        IQueryPayloadParser parser) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(parser);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IQueryPayloadParser>(parser));
        return builder;
    }

    /// <summary>
    /// Scans the specified assemblies and registers all concrete classes inheriting from <see cref="QuerySchema{T}"/> as singletons.
    /// </summary>
    [RequiresUnreferencedCode("Assembly scanning requires dynamic reflection and may not be compatible with Native AOT/Trimming.")]
    [RequiresDynamicCode("Assembly scanning creates types dynamically and may not be compatible with Native AOT.")]
    public static IQueryingBuilder AddSchemasFromAssemblies(
        this IQueryingBuilder builder,
        params Assembly[] assemblies) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(assemblies);

        for(int a = 0; a < assemblies.Length; a++) {
            Assembly? assembly = assemblies[a];
            if(assembly is null) {
                continue;
            }

            Type[] types = assembly.GetTypes();
            for(int i = 0; i < types.Length; i++) {
                Type type = types[i];
                if(type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition) {
                    continue;
                }

                Type? baseType = type.BaseType;
                while(baseType != null) {
                    if(baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(QuerySchema<>)) {
                        Type serviceType = baseType;

                        builder.Services.TryAddSingleton(type);
                        builder.Services.TryAddSingleton(serviceType, sp => sp.GetRequiredService(type));
                        break;
                    }
                    baseType = baseType.BaseType;
                }
            }
        }

        return builder;
    }

    /// <summary>
    /// Scans the specified assembly and registers all concrete classes inheriting from <see cref="QuerySchema{T}"/> as singletons.
    /// </summary>
    [RequiresUnreferencedCode("Assembly scanning requires dynamic reflection and may not be compatible with Native AOT/Trimming.")]
    [RequiresDynamicCode("Assembly scanning creates types dynamically and may not be compatible with Native AOT.")]
    public static IQueryingBuilder AddSchemasFromAssembly(
        this IQueryingBuilder builder,
        Assembly assembly) {
        return builder.AddSchemasFromAssemblies(assembly);
    }

    /// <summary>
    /// Scans the assembly containing <typeparamref name="TMarker"/> and registers all query schemas as singletons.
    /// </summary>
    [RequiresUnreferencedCode("Assembly scanning requires dynamic reflection and may not be compatible with Native AOT/Trimming.")]
    [RequiresDynamicCode("Assembly scanning creates types dynamically and may not be compatible with Native AOT.")]
    public static IQueryingBuilder AddSchemasFromAssemblyContaining<TMarker>(this IQueryingBuilder builder) {
        return builder.AddSchemasFromAssemblies(typeof(TMarker).Assembly);
    }
}