//using Microsoft.Extensions.DependencyInjection;
//using System.Linq.Expressions;
//using System.Reflection;

//namespace Wiaoj.TracingMediator.Internal;
//internal static class PipelineCompiler {
//    public static object CompileHandler(Type requestType, Type responseType, Type handlerType, List<Type> behaviorTypes) {
//        ParameterExpression spParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
//        ParameterExpression reqParam = Expression.Parameter(typeof(object), "request");
//        ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

//        UnaryExpression typedRequest = Expression.Convert(reqParam, requestType);

//        // Handler Instance
//        MethodCallExpression handlerInstance = Expression.Call(
//            typeof(ServiceProviderServiceExtensions),
//            nameof(ServiceProviderServiceExtensions.GetRequiredService),
//            [handlerType],
//            spParam
//        );

//        MethodInfo handleMethod = handlerType.GetMethod(nameof(IRequestHandler<,>.HandleAsync))!;
//        Expression next = Expression.Call(handlerInstance, handleMethod, typedRequest, ctParam);

//        // Behaviors (Reverse Wrap)
//        foreach(Type? behaviorType in behaviorTypes.AsEnumerable().Reverse()) {
//            MethodCallExpression behaviorInstance = Expression.Call(
//                typeof(ServiceProviderServiceExtensions),
//                nameof(ServiceProviderServiceExtensions.GetRequiredService),
//                [behaviorType],
//                spParam
//            );

//            MethodInfo behaviorHandleMethod = behaviorType.GetMethod(nameof(IPipelineBehavior<,>.Handle))!;
//            Type delegateType = typeof(RequestHandlerDelegate<>).MakeGenericType(responseType);
//            LambdaExpression nextLambda = Expression.Lambda(delegateType, next);

//            next = Expression.Call(behaviorInstance, behaviorHandleMethod, typedRequest, nextLambda, ctParam);
//        }

//        LambdaExpression finalLambda = Expression.Lambda(next, spParam, reqParam, ctParam);
//        return finalLambda.Compile();
//    }
//}

using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Reflection;

namespace Wiaoj.Mediator.Internal;

internal static class PipelineCompiler {
    public static object CompileRequestHandler(Type requestType,
                                               Type responseType,
                                               Type handlerType,
                                               List<Type> behaviorTypes,
                                               bool hasExceptionHandler) {
        (ParameterExpression spParam,
            ParameterExpression reqParam,
            ParameterExpression ctParam,
            UnaryExpression typedRequest) = CreateParameters(requestType);

        // 1. Handler Instance Al
        Expression handlerInstance = CreateServiceResolution(handlerType, spParam);

        // 2. Handle Metodunu Çağır (Task<TResponse> döner)
        MethodInfo handleMethod = handlerType.GetMethod(nameof(IRequestHandler<,>.HandleAsync))!;
        Expression next = Expression.Call(handlerInstance, handleMethod, typedRequest, ctParam);

        // 3. Behavior'ları Tersten Sar (Reverse Wrap)
        foreach(Type behaviorType in behaviorTypes.AsEnumerable().Reverse()) {
            Expression behaviorInstance = CreateServiceResolution(behaviorType, spParam);
            MethodInfo behaviorMethod = behaviorType.GetMethod(nameof(IPipelineBehavior<,>.Handle))!;

            Type delegateType = typeof(RequestHandlerDelegate<>).MakeGenericType(responseType);
            LambdaExpression nextLambda = Expression.Lambda(delegateType, next);

            next = Expression.Call(behaviorInstance, behaviorMethod, typedRequest, nextLambda, ctParam);
        }

        if(hasExceptionHandler) {
            // Parametre: Exception ex
            ParameterExpression exceptionVar = Expression.Parameter(typeof(Exception), "exception");

            // IRequestExceptionHandler<TReq, TRes, Exception> arayüzünü al
            Type exceptionHandlerType = typeof(IRequestExceptionHandler<,,>).MakeGenericType(requestType, responseType, typeof(Exception));

            // serviceProvider.GetRequiredService<IRequestExceptionHandler...>()
            Expression ehInstance = CreateServiceResolution(exceptionHandlerType, spParam);

            // HandleAsync(request, ex, ct) metodunu bul
            MethodInfo ehMethod = exceptionHandlerType.GetMethod(nameof(IRequestExceptionHandler<,,>.HandleAsync))!;

            // Handler çağrısı (Task döner)
            Expression ehCall = Expression.Call(ehInstance, ehMethod, typedRequest, exceptionVar, ctParam);

            // Exception Handler'dan sonra ne dönülecek? 
            // Genelde EH void/Task döner ve akış biter (rethrow yapmazsa). 
            // Ancak Response dönmemiz lazım. Burada basitlik adına "default(TResponse)" dönüyoruz 
            // veya rethrow yapıyoruz.
            // Bu örnekte: EH çalıştırılır, sonra hata fırlatılır (logging amaçlı EH). 
            // Eğer hatayı yutup default dönmek istiyorsan burası değişmeli.

            BlockExpression catchBlock = Expression.Block(
                ehCall, // Önce handle et
                Expression.Rethrow(responseType) // Sonra fırlat (veya default değer dön)
            );

            next = Expression.TryCatch(
                next,
                Expression.Catch(exceptionVar, catchBlock)
            );
        }

        return Expression.Lambda(next, spParam, reqParam, ctParam).Compile();
    }

    public static object CompileStreamHandler(Type requestType, Type responseType, Type handlerType) {
        // Not: Stream pipeline için Behavior desteği genelde karmaşıktır ve yield return mantığına terstir.
        // Şimdilik sadece Handler'ı çağırıp stream'i döndürüyoruz.
        // Eğer Stream Behavior istenirse (örn: Logging) IAsyncEnumerable sarmalayan özel bir yapı gerekir.

        (ParameterExpression spParam,
            ParameterExpression reqParam,
            ParameterExpression ctParam,
            UnaryExpression typedRequest) = CreateParameters(requestType);

        Expression handlerInstance = CreateServiceResolution(handlerType, spParam);

        // IStreamRequestHandler<TReq, TRes>.Handle metodunu bul
        // Handle metodu IAsyncEnumerable<TResponse> döner, Task DEĞİL.
        MethodInfo handleMethod = handlerType.GetMethod(nameof(IStreamRequestHandler<,>.Handle))!;

        Expression body = Expression.Call(handlerInstance, handleMethod, typedRequest, ctParam);

        return Expression.Lambda(body, spParam, reqParam, ctParam).Compile();
    }

    private static HandlerExpressionParameters CreateParameters(Type requestType) {
        ParameterExpression spParam = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        ParameterExpression reqParam = Expression.Parameter(typeof(object), "request");
        ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        UnaryExpression typedRequest = Expression.Convert(reqParam, requestType);
        return new HandlerExpressionParameters(spParam, reqParam, ctParam, typedRequest);
    }

    private static MethodCallExpression CreateServiceResolution(Type serviceType, ParameterExpression spParam) {
        return Expression.Call(
            typeof(ServiceProviderServiceExtensions),
            nameof(ServiceProviderServiceExtensions.GetRequiredService),
            [serviceType],
            spParam
        );
    }

    /// <summary>
    /// Holds the standard parameters required for generating dynamic request handler delegates.
    /// </summary>
    private readonly record struct HandlerExpressionParameters(
        ParameterExpression ServiceProvider,
        ParameterExpression Request,
        ParameterExpression CancellationToken,
        UnaryExpression TypedRequest);
}