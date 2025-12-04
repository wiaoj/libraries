 
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Wiaoj.Threading.Channels;  

namespace Wiaoj.Concurrency.Pipelines;

/// <summary>
/// Provides a fluent API for building and running a multi-stage, asynchronous data processing pipeline.
/// </summary>
public static class AsyncPipeline {
    /// <summary>
    /// Creates a new pipeline builder, starting with a source of data.
    /// </summary>
    /// <typeparam name="TIn">The type of data from the source.</typeparam>
    /// <param name="source">An asynchronous enumerable sequence of data to feed into the pipeline.</param>
    /// <returns>A pipeline builder instance to define the first stage.</returns>
    public static IAsyncPipelineBuilder<TIn> CreateFor<TIn>(IAsyncEnumerable<TIn> source) {
        return new AsyncPipelineBuilder<TIn>(source);
    }
}

/// <summary>
/// Defines the contract for a pipeline builder that has defined stages.
/// </summary>
public interface IAsyncPipelineRunner {
    /// <summary>
    /// Runs the entire configured pipeline and waits for it to complete.
    /// </summary>
    /// <param name="cancellationToken">A token to signal cancellation for the entire pipeline.</param>
    /// <returns>A task that completes when the pipeline finishes, fails, or is cancelled.</returns>
    Task RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the contract for building the stages of a pipeline.
/// </summary>
/// <typeparam name="TOut">The output type of the current stage.</typeparam>
public interface IAsyncPipelineBuilder<TOut> {
    /// <summary>
    /// Adds a new processing stage to the pipeline.
    /// </summary>
    /// <typeparam name="TNextOut">The output type of the new stage.</typeparam>
    /// <param name="step">A function that transforms the async enumerable from the previous stage.</param>
    /// <param name="options">Optional configuration for the channel connecting this stage.</param>
    /// <returns>A new builder instance representing the extended pipeline.</returns>
    IAsyncPipelineBuilder<TNextOut> AddStep<TNextOut>(Func<IAsyncEnumerable<TOut>, CancellationToken, IAsyncEnumerable<TNextOut>> step, BoundedChannelOptions? options = null);

    /// <summary>
    /// Adds the final processing stage (a consumer) to the pipeline, which does not produce further output.
    /// </summary>
    /// <param name="finalStep">An action that consumes the async enumerable output of the final stage.</param>
    /// <returns>An executable pipeline runner.</returns>
    IAsyncPipelineRunner AddFinalStep(Func<IAsyncEnumerable<TOut>, CancellationToken, Task> finalStep);
}


// --- IMPLEMENTATION ---

internal class AsyncPipelineBuilder<TCurrentOut> : IAsyncPipelineBuilder<TCurrentOut> {
    private readonly Func<CancellationToken, Task> _pipelineRunner;

    // First stage constructor
    public AsyncPipelineBuilder(IAsyncEnumerable<TCurrentOut> source) {
        // The initial runner just feeds the source into a channel.
        _pipelineRunner = async (ct) => {
            await using var channel = AsyncChannel<TCurrentOut>.CreateBounded(100);

            var feeder = Task.Run(async () => {
                await foreach (var item in source.WithCancellation(ct)) {
                    await channel.Writer.WriteAsync(item, ct);
                }
            }, ct);

            // This is the starting point, so we just complete the feeder.
            await feeder;
        };
    }

    // Subsequent stages constructor
    private AsyncPipelineBuilder(Func<CancellationToken, Task> previousRunner) {
        _pipelineRunner = previousRunner;
    }

    public IAsyncPipelineBuilder<TNextOut> AddStep<TNextOut>(
        Func<IAsyncEnumerable<TCurrentOut>, CancellationToken, IAsyncEnumerable<TNextOut>> step,
        BoundedChannelOptions? options = null) {
        // Chain the new step to the previous runner
        Func<CancellationToken, Task> newRunner = async (ct) => {
            await using var inputChannel = AsyncChannel<TCurrentOut>.CreateBounded(options ?? new BoundedChannelOptions(100));
            await using var outputChannel = AsyncChannel<TNextOut>.CreateBounded(options ?? new BoundedChannelOptions(100));

            // Start the previous part of the pipeline, feeding into our input channel
            var previousTask = _pipelineRunner(ct);

            // Start the current step's work
            var currentTask = Task.Run(async () => {
                var transformed = step(inputChannel.Reader.ReadAllAsync(ct), ct);
                await foreach (var item in transformed.WithCancellation(ct)) {
                    await outputChannel.Writer.WriteAsync(item, ct);
                }
            }, ct);

            await Task.WhenAll(previousTask, currentTask);
        };

        return new AsyncPipelineBuilder<TNextOut>(newRunner);
    }

    public IAsyncPipelineRunner AddFinalStep(Func<IAsyncEnumerable<TCurrentOut>, CancellationToken, Task> finalStep) {
        Func<CancellationToken, Task> finalRunner = async (ct) => {
            await using var inputChannel = AsyncChannel<TCurrentOut>.CreateBounded(100);

            var previousTask = _pipelineRunner(ct);
            var finalTask = Task.Run(() => finalStep(inputChannel.Reader.ReadAllAsync(ct), ct), ct);

            await Task.WhenAll(previousTask, finalTask);
        };

        return new AsyncPipelineRunner(finalRunner);
    }
}

internal class AsyncPipelineRunner : IAsyncPipelineRunner {
    private readonly Func<CancellationToken, Task> _runner;

    public AsyncPipelineRunner(Func<CancellationToken, Task> runner) {
        _runner = runner;
    }

    public Task RunAsync(CancellationToken cancellationToken = default) {
        return _runner(cancellationToken);
    }
}