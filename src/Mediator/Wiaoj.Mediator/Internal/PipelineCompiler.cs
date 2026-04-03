using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Reflection;

namespace Wiaoj.Mediator.Internal;

internal static class PipelineCompiler {
    // ─────────────────────────────────────────────────────────────────────────
    // Request Handler (behaviors + handler core)
    // Exception handling and processors are layered by PipelineWrappers AFTER
    // this method returns — they require async/await which expression trees
    // cannot express directly.
    // ─────────────────────────────────────────────────────────────────────────

    public static object CompileRequestHandler(
        Type requestType,
        Type responseType,
        Type handlerType,
        IReadOnlyList<Type> behaviorTypes) {

        (ParameterExpression spParam,
         ParameterExpression reqParam,
         ParameterExpression ctParam,
         UnaryExpression typedRequest) = CreateParameters(requestType);

        // 1. Resolve handler and invoke Handle
        Expression handlerInstance = CreateServiceResolution(handlerType, spParam);
        
        // Cast instance to the interface — handles multiple handlers and explicit implementations correctly.
        Type handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
        MethodInfo handleMethod = GetMethodOrThrow(handlerInterface, nameof(IRequestHandler<,>.Handle));
        Expression castInstance = Expression.Convert(handlerInstance, handlerInterface);
        
        Expression next = Expression.Call(castInstance, handleMethod, typedRequest, ctParam);

        // 2. Wrap behaviors in reverse (outermost = first registered)
        foreach(Type behaviorType in behaviorTypes.Reverse()) {
            Expression behaviorInstance = CreateServiceResolution(behaviorType, spParam);
            MethodInfo behaviorMethod = GetMethodOrThrow(behaviorType, nameof(IPipelineBehavior<,>.Handle));

            Type delegateType = typeof(RequestHandlerDelegate<>).MakeGenericType(responseType);
            LambdaExpression nextLambda = Expression.Lambda(delegateType, next);

            next = Expression.Call(behaviorInstance, behaviorMethod, typedRequest, nextLambda, ctParam);
        }

        return Expression.Lambda(next, spParam, reqParam, ctParam).Compile();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stream Handler
    // ─────────────────────────────────────────────────────────────────────────

    public static object CompileStreamHandler(Type requestType, Type responseType, Type handlerType) {
        (ParameterExpression spParam,
         ParameterExpression reqParam,
         ParameterExpression ctParam,
         UnaryExpression typedRequest) = CreateParameters(requestType);

        Expression handlerInstance = CreateServiceResolution(handlerType, spParam);
        
        // Cast instance to the interface — handles multiple handlers and explicit implementations correctly.
        Type handlerInterface = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, responseType);
        MethodInfo handleMethod = GetMethodOrThrow(handlerInterface, nameof(IStreamRequestHandler<,>.Handle));
        Expression castInstance = Expression.Convert(handlerInstance, handlerInterface);
        
        Expression body = Expression.Call(castInstance, handleMethod, typedRequest, ctParam);

        return Expression.Lambda(body, spParam, reqParam, ctParam).Compile();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pre-Processor Delegate
    // Returns: Func<IServiceProvider, object /*req*/, CancellationToken, Task>
    // ─────────────────────────────────────────────────────────────────────────

    public static Func<IServiceProvider, object, CancellationToken, Task>
        CompilePreProcessorDelegate(Type processorType, Type requestType, Type responseType) {

        ParameterExpression spParam = Expression.Parameter(typeof(IServiceProvider), Constants.ServiceProvider);
        ParameterExpression reqParam = Expression.Parameter(typeof(object), Constants.Request);
        ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), Constants.CancellationToken);

        UnaryExpression typedRequest = Expression.Convert(reqParam, requestType);
        Expression instance = CreateServiceResolution(processorType, spParam);

        // Cast instance to the interface — handles explicit interface implementations correctly.
        Type preInterface = typeof(IRequestPreProcessor<,>).MakeGenericType(requestType, responseType);
        MethodInfo ifaceMethod = GetMethodOrThrow(preInterface, nameof(IRequestPreProcessor<,>.Process));
        Expression castInstance = Expression.Convert(instance, preInterface);
        Expression call = Expression.Call(castInstance, ifaceMethod, typedRequest, ctParam);

        return Expression.Lambda<Func<IServiceProvider, object, CancellationToken, Task>>(
            call, spParam, reqParam, ctParam).Compile();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Post-Processor Delegate
    // Returns: Func<IServiceProvider, object /*req*/, TResponse, CancellationToken, Task>
    // Boxed as object — PipelineWrappers.WrapWithProcessorsCore casts at startup.
    // ─────────────────────────────────────────────────────────────────────────

    public static object CompilePostProcessorDelegate(
        Type processorType, Type requestType, Type responseType) {

        ParameterExpression spParam = Expression.Parameter(typeof(IServiceProvider), Constants.ServiceProvider);
        ParameterExpression reqParam = Expression.Parameter(typeof(object), Constants.Request);
        ParameterExpression resParam = Expression.Parameter(responseType, Constants.Response);
        ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), Constants.CancellationToken);

        UnaryExpression typedRequest = Expression.Convert(reqParam, requestType);
        Expression instance = CreateServiceResolution(processorType, spParam);

        Type postInterface = typeof(IRequestPostProcessor<,>).MakeGenericType(requestType, responseType);
        MethodInfo ifaceMethod = GetMethodOrThrow(postInterface, nameof(IRequestPostProcessor<,>.Process));
        Expression castInstance = Expression.Convert(instance, postInterface);
        Expression call = Expression.Call(castInstance, ifaceMethod, typedRequest, resParam, ctParam);

        // Delegate type: Func<IServiceProvider, object, TResponse, CancellationToken, Task>
        Type delegateType = typeof(Func<,,,,>).MakeGenericType(
            typeof(IServiceProvider), typeof(object), responseType, typeof(CancellationToken), typeof(Task));

        return Expression.Lambda(delegateType, call, spParam, reqParam, resParam, ctParam).Compile();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static HandlerExpressionParameters CreateParameters(Type requestType) {
        ParameterExpression spParam = Expression.Parameter(typeof(IServiceProvider), Constants.ServiceProvider);
        ParameterExpression reqParam = Expression.Parameter(typeof(object), Constants.Request);
        ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), Constants.CancellationToken);
        UnaryExpression typedRequest = Expression.Convert(reqParam, requestType);
        return new HandlerExpressionParameters(spParam, reqParam, ctParam, typedRequest);
    }

    private static MethodCallExpression CreateServiceResolution(
        Type serviceType, ParameterExpression spParam) {
        return Expression.Call(typeof(ServiceProviderServiceExtensions),
                               nameof(ServiceProviderServiceExtensions.GetRequiredService),
                               [serviceType],
                               spParam);
    }

    /// <summary>
    /// Resolves a method by name from <paramref name="type"/> and throws a clear
    /// <see cref="InvalidOperationException"/> if it cannot be found — instead of
    /// propagating a silent <see cref="NullReferenceException"/> from the call site.
    /// </summary>
    private static MethodInfo GetMethodOrThrow(Type type, string methodName) {
        return type.GetMethod(methodName)
            ?? throw new InvalidOperationException(
                $"Method '{methodName}' was not found on '{type.FullName}'. " +
                $"This is likely a Wiaoj.Mediator bug — please open an issue.");
    }

    private readonly record struct HandlerExpressionParameters(
        ParameterExpression ServiceProvider,
        ParameterExpression Request,
        ParameterExpression CancellationToken,
        UnaryExpression TypedRequest);
}