using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Wiaoj.Mediator.Internal;
/// <summary>
/// Provides startup-time compilation of async pipeline wrappers for exception handlers and processors.
/// <para>
/// <b>Design note:</b> Expression trees cannot use <c>await</c>.  The core pipeline
/// (behaviors + handler) is compiled with <see cref="PipelineCompiler"/> as pure expression trees.
/// Exception handling and processors require proper async chaining, so they are wrapped here
/// using regular <c>async</c> lambdas.  Each wrapper method is invoked ONCE per handler type
/// via <see cref="MethodInfo.MakeGenericMethod"/> at startup; the resulting
/// delegate is cached in <see cref="HandlerRegistry"/> and called at zero allocation cost at runtime.
/// </para>
/// </summary>
[DebuggerStepThrough, DebuggerNonUserCode]
internal static class PipelineWrappers {

    // ─────────────────────────────────────────────────────────────────────────
    // Exception Handler Wrapping
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps <paramref name="innerDelegate"/> with a try/catch that calls the registered
    /// <see cref="IRequestExceptionHandler{TRequest,TResponse,TException}"/>.
    /// </summary>
    /// <param name="innerDelegate">The expression-tree-compiled pipeline delegate (boxed).</param>
    /// <param name="requestType">Closed request type.</param>
    /// <param name="responseType">Closed response type.</param>
    /// <param name="exceptionHandlerInterfaceType">
    /// The closed <c>IRequestExceptionHandler&lt;TRequest,TResponse,Exception&gt;</c> service type.
    /// </param>
    /// <returns>A new boxed delegate wrapping <paramref name="innerDelegate"/>.</returns>
    public static object WrapWithExceptionHandler(
        object innerDelegate,
        Type requestType,
        Type responseType,
        Type exceptionHandlerInterfaceType) {

        // Added BindingFlags to ensure we can find the method even if it's private/internal
        return typeof(PipelineWrappers)
            .GetMethod(nameof(WrapWithExceptionHandlerCore), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(requestType, responseType)
            .Invoke(null, [innerDelegate, exceptionHandlerInterfaceType])!;
    }

    /// <summary>
    /// Generic core — called once at startup via MakeGenericMethod; result is cached.
    /// </summary>
    private static Func<IServiceProvider, object, CancellationToken, Task<TResponse>>
        WrapWithExceptionHandlerCore<TRequest, TResponse>(
            Func<IServiceProvider, object, CancellationToken, Task<TResponse>> inner,
            Type exceptionHandlerInterfaceType)
        where TRequest : IRequest<TResponse> {

        return async (sp, req, cancellationToken) => {
            try {
                return await inner(sp, req, cancellationToken).ConfigureAwait(false);
            }
            catch(Exception ex) {
                IRequestExceptionHandler<TRequest, TResponse, Exception> handler =
                    (IRequestExceptionHandler<TRequest, TResponse, Exception>)sp.GetRequiredService(exceptionHandlerInterfaceType);

                ExceptionContext<TResponse> context = new();
                await handler.Handle((TRequest)req, ex, context, cancellationToken).ConfigureAwait(false);

                if(context.IsHandled)
                    return context.Result;

                // Preserve the original stack trace.
                ExceptionDispatchInfo.Capture(ex).Throw();
                return default!; // unreachable
            }
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pre / Post Processor Wrapping
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps <paramref name="innerDelegate"/> with pre-processor calls (before) and
    /// post-processor calls (after) the inner pipeline.
    /// </summary>
    public static object WrapWithProcessors(
        object innerDelegate,
        Type requestType,
        Type responseType,
        IReadOnlyList<Type> preProcessorTypes,
        IReadOnlyList<Type> postProcessorTypes) {

        // Compile each processor into a strongly-typed delegate (expression tree, no reflection at runtime).
        Func<IServiceProvider, object, CancellationToken, Task>[] preCompiled =
            preProcessorTypes
                .Select(t => PipelineCompiler.CompilePreProcessorDelegate(t, requestType, responseType))
                .ToArray();

        object[] postCompiled =
            postProcessorTypes
                .Select(t => PipelineCompiler.CompilePostProcessorDelegate(t, requestType, responseType))
                .ToArray();

        return typeof(PipelineWrappers)
            .GetMethod(nameof(WrapWithProcessorsCore), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(responseType)
            .Invoke(null, [innerDelegate, preCompiled, postCompiled])!;
    }

    /// <summary>
    /// Generic core — called once at startup via MakeGenericMethod; result is cached.
    /// </summary>
    private static Func<IServiceProvider, object, CancellationToken, Task<TResponse>>
        WrapWithProcessorsCore<TResponse>(
            Func<IServiceProvider, object, CancellationToken, Task<TResponse>> inner,
            Func<IServiceProvider, object, CancellationToken, Task>[] preProcessors,
            object[] rawPostProcessors) {

        // Cast once at startup, not at runtime.
        Func<IServiceProvider, object, TResponse, CancellationToken, Task>[] postProcessors =
            rawPostProcessors
                .Cast<Func<IServiceProvider, object, TResponse, CancellationToken, Task>>()
                .ToArray();

        // Fast-path: Skip async state machine overhead if there are no processors
        if(preProcessors.Length == 0 && postProcessors.Length == 0)
            return inner;

        return async (sp, req, cancellationToken) => {
            for(int i = 0; i < preProcessors.Length; i++)
                await preProcessors[i](sp, req, cancellationToken).ConfigureAwait(false);

            TResponse result = await inner(sp, req, cancellationToken).ConfigureAwait(false);

            for(int i = 0; i < postProcessors.Length; i++)
                await postProcessors[i](sp, req, result, cancellationToken).ConfigureAwait(false);

            return result;
        };
    }
}