using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using Wiaoj.Mediator;
using Wiaoj.Mediator.Behaviors;
using Wiaoj.Mediator.Internal;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for registering Wiaoj Mediator services.
/// </summary>
public static class ServiceCollectionExtensions {

    /// <summary>
    /// Registers Wiaoj Mediator: handlers, processors, behaviors, exception handlers, and the mediator itself.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Fluent configuration delegate.</param>
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        Action<IMediatorBuilder> configure) {

        MediatorBuilder builder = new(services);
        configure(builder);

        // ── Collect all candidate types ──────────────────────────────────────
        List<Assembly> assemblies =
            [.. builder.AssemblyMarkers.Select(t => t.Assembly).Distinct()];

        List<Type> allTypes =
            [.. assemblies.SelectMany(a => a.GetTypes()).Where(t => t.IsClass && !t.IsAbstract)];

        // Merge manual registrations
        allTypes.AddRange(builder.ManualHandlers);
        allTypes.AddRange(builder.ManualPreProcessors);
        allTypes.AddRange(builder.ManualPostProcessors);
        allTypes = [.. allTypes.Distinct()];

        // ── Discover components ──────────────────────────────────────────────
        List<HandlerDescriptor> handlerDescriptors = FindHandlers(allTypes);
        List<ExceptionHandlerDescriptor> exceptionHandlers = FindExceptionHandlers(allTypes);
        List<ProcessorDescriptor> preProcessorDescs = FindPreProcessors(allTypes);
        List<ProcessorDescriptor> postProcessorDescs = FindPostProcessors(allTypes);

        // ── Register exception handlers in DI ────────────────────────────────
        foreach(ExceptionHandlerDescriptor eh in exceptionHandlers)
            services.TryAddScoped(eh.Interface, eh.Implementation);

        // ── Register processors in DI ────────────────────────────────────────
        foreach(ProcessorDescriptor pd in preProcessorDescs)
            services.TryAdd(new ServiceDescriptor(pd.Implementation, pd.Implementation, builder.DefaultLifetime));

        foreach(ProcessorDescriptor pd in postProcessorDescs)
            services.TryAdd(new ServiceDescriptor(pd.Implementation, pd.Implementation, builder.DefaultLifetime));

        // ── Build and register handler pipeline delegates ────────────────────
        HandlerRegistry registry = new();

        foreach(HandlerDescriptor h in handlerDescriptors) {
            // Register handler in DI
            services.TryAdd(new ServiceDescriptor(h.Implementation, h.Implementation, builder.DefaultLifetime));

            if(h.IsStream) {
                object compiled = PipelineCompiler.CompileStreamHandler(h.RequestType, h.ResponseType, h.Implementation);
                registry.RegisterStream(h.RequestType, compiled);
                continue;
            }

            // ── Build behavior list ──────────────────────────────────────────
            List<Type> validBehaviors = BuildBehaviorList(builder, services, h);

            // ── Compile core pipeline (expression tree) ──────────────────────
            object pipeline = PipelineCompiler.CompileRequestHandler(
                h.RequestType, h.ResponseType, h.Implementation, validBehaviors);

            // ── Layer: exception handler ─────────────────────────────────────
            Type exceptionHandlerInterface = typeof(IRequestExceptionHandler<,,>)
                .MakeGenericType(h.RequestType, h.ResponseType, typeof(Exception));

            bool hasExceptionHandler = exceptionHandlers
                .Any(x => exceptionHandlerInterface.IsAssignableFrom(x.Interface));

            if(hasExceptionHandler) {
                pipeline = PipelineWrappers.WrapWithExceptionHandler(
                    pipeline, h.RequestType, h.ResponseType, exceptionHandlerInterface);
            }

            // ── Layer: pre / post processors ─────────────────────────────────
            List<Type> matchedPre = MatchProcessors(preProcessorDescs, h.RequestType, h.ResponseType);
            List<Type> matchedPost = MatchProcessors(postProcessorDescs, h.RequestType, h.ResponseType);

            if(matchedPre.Count > 0 || matchedPost.Count > 0) {
                pipeline = PipelineWrappers.WrapWithProcessors(
                    pipeline, h.RequestType, h.ResponseType, matchedPre, matchedPost);
            }

            registry.Register(h.RequestType, pipeline);
        }

        registry.ToFrozen();
        services.AddSingleton(registry);

        // ── Register mediator implementation ─────────────────────────────────
        if(builder.IsTracingEnabled)
            services.TryAddScoped<IMediator, TracingMediator>();
        else
            services.TryAddScoped<IMediator, Mediator>();

        services.TryAddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());

        return services;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Behavior list construction (user-defined + attribute-driven built-ins)
    // ─────────────────────────────────────────────────────────────────────────

    private static List<Type> BuildBehaviorList(
        MediatorBuilder builder, IServiceCollection services, HandlerDescriptor h) {

        List<Type> validBehaviors = [];

        // 1. User-defined open/command/query behaviors
        foreach(BehaviorRegistryItem behaviorItem in builder.Behaviors) {
            if(!IsCategoryMatch(h.RequestType, behaviorItem.Category))
                continue;

            if(!IsCompatible(behaviorItem.Type, h.RequestType, h.ResponseType))
                continue;

            Type closedBehavior = behaviorItem.Type.MakeGenericType(h.RequestType, h.ResponseType);
            validBehaviors.Add(closedBehavior);
            services.TryAdd(new ServiceDescriptor(closedBehavior, closedBehavior, behaviorItem.Lifetime));
        }

        // 2. Attribute-driven built-in behaviors — auto-injected, no user config needed
        TryAddAttributeBehavior<RetryAttribute>(
            h, services, typeof(RetryBehavior<,>), validBehaviors);

        TryAddAttributeBehavior<TimeoutAttribute>(
            h, services, typeof(TimeoutBehavior<,>), validBehaviors);

        TryAddAttributeBehavior<RateLimitAttribute>(
            h, services, typeof(RateLimitBehavior<,>), validBehaviors);

        return validBehaviors;
    }

    private static void TryAddAttributeBehavior<TAttribute>(
        HandlerDescriptor h,
        IServiceCollection services,
        Type openBehaviorType,
        List<Type> validBehaviors)
        where TAttribute : Attribute {

        if(h.RequestType.GetCustomAttribute<TAttribute>(inherit: false) is null)
            return;

        Type closed = openBehaviorType.MakeGenericType(h.RequestType, h.ResponseType);
        validBehaviors.Add(closed);
        services.TryAdd(new ServiceDescriptor(closed, closed, ServiceLifetime.Singleton));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Discovery helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static List<HandlerDescriptor> FindHandlers(List<Type> allTypes) {
        IEnumerable<HandlerDescriptor> handlers = allTypes
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                .Select(i => new HandlerDescriptor(
                    i.GetGenericArguments()[0],
                    i.GetGenericArguments()[1],
                    t,
                    IsStream: false)));

        IEnumerable<HandlerDescriptor> streamHandlers = allTypes
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequestHandler<,>))
                .Select(i => new HandlerDescriptor(
                    i.GetGenericArguments()[0],
                    i.GetGenericArguments()[1],
                    t,
                    IsStream: true)));

        return handlers.Concat(streamHandlers).ToList();
    }

    private static List<ExceptionHandlerDescriptor> FindExceptionHandlers(List<Type> allTypes) {
        return [.. allTypes.SelectMany(t => t.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestExceptionHandler<,,>))
            .Select(i => new ExceptionHandlerDescriptor(i, t)))];
    }

    private static List<ProcessorDescriptor> FindPreProcessors(List<Type> allTypes) {
        return [.. allTypes.SelectMany(t => 
                t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestPreProcessor<,>))
            .Select(i => new ProcessorDescriptor(
                i.GetGenericArguments()[0], 
                i.GetGenericArguments()[1], 
                t)))];
    }

    private static List<ProcessorDescriptor> FindPostProcessors(List<Type> allTypes) {
        return [.. allTypes.SelectMany(
                    t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestPostProcessor<,>))
            .Select(i => new ProcessorDescriptor(
                i.GetGenericArguments()[0],
                i.GetGenericArguments()[1],
                t)))];
    }

    /// <summary>
    /// Returns processor implementation types that are compatible with
    /// the given request/response type pair.
    /// Supports exact match and base-type polymorphism.
    /// </summary>
    private static List<Type> MatchProcessors(
        List<ProcessorDescriptor> descriptors, Type requestType, Type responseType) {
        return [.. descriptors
            .Where(p => p.RequestType.IsAssignableFrom(requestType) && p.ResponseType == responseType)
            .Select(p => p.Implementation)];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Behavior compatibility
    // ─────────────────────────────────────────────────────────────────────────

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
        return givenType.GetInterfaces()
            .Any(it => 
                it.IsGenericType 
                && it.GetGenericTypeDefinition() == genericType)
                || (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType);
    }

    private static bool IsCompatible(Type openBehaviorType, Type requestType, Type responseType) {
        Type[] args = openBehaviorType.GetGenericArguments();
        Type requestParam = args[0];
        Type[] constraints = requestParam.GetGenericParameterConstraints();

        foreach(Type constraint in constraints) {
            Type check = constraint;
            if(constraint.IsGenericType && constraint.ContainsGenericParameters)
                check = constraint.GetGenericTypeDefinition().MakeGenericType(responseType);

            if(!check.IsAssignableFrom(requestType))
                return false;
        }
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Descriptors (internal records)
    // ─────────────────────────────────────────────────────────────────────────

    private sealed record HandlerDescriptor(Type RequestType, Type ResponseType, Type Implementation, bool IsStream);
    private sealed record ExceptionHandlerDescriptor(Type Interface, Type Implementation);
    private sealed record ProcessorDescriptor(Type RequestType, Type ResponseType, Type Implementation);
}