using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using Wiaoj.Mediator;
using Wiaoj.Mediator.Internal;

#pragma warning disable IDE0130 
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 
/// <summary>
/// Extension methods for setting up the Mediator in an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Registers Wiaoj Mediator services into the specified <see cref="IServiceCollection"/>.
    /// This includes registering handlers, pipelines, and the Mediator instance itself.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">A delegate to configure the Mediator options (e.g., adding handlers, behaviors, or enabling tracing).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddMediator(this IServiceCollection services, Action<IMediatorBuilder> configure) {
        MediatorBuilder builder = new(services);
        configure(builder);

        HandlerRegistry registry = new();

        List<Assembly> assemblies = [.. builder.AssemblyMarkers.Select(t => t.Assembly).Distinct()];
        List<Type> allTypes = [.. assemblies.SelectMany(a => a.GetTypes()).Where(t => t.IsClass && !t.IsAbstract)];

        if(builder.ManualHandlers.Count > 0) {
            allTypes.AddRange(builder.ManualHandlers);
        }
        allTypes = allTypes.Distinct().ToList();

        List<HandlerDescriptor> handlerDescriptors = FindHandlers(allTypes);
        List<ExceptionHandlerDescriptor> exceptionHandlers = FindExceptionHandlers(allTypes);

        foreach(ExceptionHandlerDescriptor eh in exceptionHandlers) {
            services.TryAddScoped(eh.Interface, eh.Implementation);
        }

        foreach(HandlerDescriptor h in handlerDescriptors) {
            services.TryAdd(new ServiceDescriptor(h.Implementation, h.Implementation, builder.DefaultLifetime));

            if(h.IsStream) {
                object compiled = PipelineCompiler.CompileStreamHandler(h.RequestType, h.ResponseType, h.Implementation);
                registry.RegisterStream(h.RequestType, compiled);
            }
            else {
                List<Type> validBehaviors = [];

                foreach(BehaviorRegistryItem behaviorItem in builder.Behaviors) {

                    if(!IsCategoryMatch(h.RequestType, behaviorItem.Category)) {
                        continue;
                    }

                    if(IsCompatible(behaviorItem.Type, h.RequestType, h.ResponseType)) {

                        Type closedBehavior = behaviorItem.Type.MakeGenericType(h.RequestType, h.ResponseType);
                        validBehaviors.Add(closedBehavior);

                        services.TryAdd(new ServiceDescriptor(closedBehavior, closedBehavior, behaviorItem.Lifetime));
                    }
                }

                Type exceptionHandlerInterface = typeof(IRequestExceptionHandler<,,>)
                    .MakeGenericType(h.RequestType, h.ResponseType, typeof(Exception));
                bool hasExceptionHandler = exceptionHandlers.Any(x => x.Interface.IsAssignableFrom(exceptionHandlerInterface));

                object compiled = PipelineCompiler.CompileRequestHandler(
                    h.RequestType,
                    h.ResponseType,
                    h.Implementation,
                    validBehaviors,
                    hasExceptionHandler
                );

                registry.Register(h.RequestType, compiled);
            }
        }

        registry.ToFrozen();
        services.AddSingleton(registry);

        if(builder.IsTracingEnabled) {
            services.TryAddScoped<IMediator, TracingMediator>();
        }
        else {
            services.TryAddScoped<IMediator, Mediator>();
        }

        services.TryAddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());

        return services;
    }

    // --- YENİ: Kategori Eşleştirme Mantığı ---
    private static bool IsCategoryMatch(Type requestType, BehaviorCategory category) {
        return category switch {
            BehaviorCategory.All => true,
            BehaviorCategory.Command => IsAssignableToGenericType(requestType, typeof(ICommand<>)),
            BehaviorCategory.Query => IsAssignableToGenericType(requestType, typeof(IQuery<>)),
            BehaviorCategory.Stream => IsAssignableToGenericType(requestType, typeof(IStreamRequest<>)),
            _ => false
        };
    }

    private static bool IsAssignableToGenericType(Type givenType, Type genericType) {
        return givenType.GetInterfaces().Any(it => it.IsGenericType && it.GetGenericTypeDefinition() == genericType)
               || (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType);
    }

    private static bool IsCompatible(Type openBehaviorType, Type requestType, Type responseType) {
        Type[] args = openBehaviorType.GetGenericArguments();
        Type requestParam = args[0];
        Type[] constraints = requestParam.GetGenericParameterConstraints();

        foreach(Type constraint in constraints) {
            Type checkConstraint = constraint;
            if(constraint.IsGenericType && constraint.ContainsGenericParameters) {
                checkConstraint = constraint.GetGenericTypeDefinition().MakeGenericType(responseType);
            }
            if(!checkConstraint.IsAssignableFrom(requestType)) return false;
        }
        return true;
    }

    private static List<HandlerDescriptor> FindHandlers(List<Type> allTypes) {
        IEnumerable<HandlerDescriptor> handlers = allTypes
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            .Select(i => new HandlerDescriptor(i.GetGenericArguments()[0], i.GetGenericArguments()[1], i.ReflectedType ?? allTypes.First(t => t.GetInterfaces().Contains(i)), false));

        IEnumerable<HandlerDescriptor> streamHandlers = allTypes
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequestHandler<,>))
            .Select(i => new HandlerDescriptor(i.GetGenericArguments()[0], i.GetGenericArguments()[1], i.ReflectedType ?? allTypes.First(t => t.GetInterfaces().Contains(i)), true));

        return handlers.Concat(streamHandlers).ToList();
    }

    private static List<ExceptionHandlerDescriptor> FindExceptionHandlers(List<Type> allTypes) {
        return allTypes
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestExceptionHandler<,,>))
            .Select(i => new ExceptionHandlerDescriptor(i, i.ReflectedType ?? allTypes.First(t => t.GetInterfaces().Contains(i))))
            .ToList();
    }

    private record HandlerDescriptor(Type RequestType, Type ResponseType, Type Implementation, bool IsStream);
    private record ExceptionHandlerDescriptor(Type Interface, Type Implementation);
}