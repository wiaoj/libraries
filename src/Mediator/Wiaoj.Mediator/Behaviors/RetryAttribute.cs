using System;
using System.Collections.Generic;
using System.Text;

namespace Wiaoj.Mediator.Behaviors;  
/// <summary>
/// Enables automatic retry logic for a request.
/// <para>
/// Apply to a request class to have the pipeline automatically retry on failure.
/// <see cref="RetryBehavior{TRequest,TResponse}"/> must be registered via
/// <c>AddOpenBehavior(typeof(RetryBehavior&lt;,&gt;))</c> or the built-in behavior scanning will add it automatically.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [Retry(count: 3, delayMs: 200, exponentialBackoff: true)]
/// public record CreateOrderCommand(...) : ICommand&lt;OrderId&gt;;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RetryAttribute : Attribute {
    /// <summary>Maximum number of retry attempts after the first failure.</summary>
    public int Count { get; }

    /// <summary>Base delay in milliseconds between retries.</summary>
    public int DelayMs { get; }

    /// <summary>
    /// If <c>true</c>, each retry delay is multiplied by 2^attempt (exponential backoff).
    /// If <c>false</c>, a fixed delay of <see cref="DelayMs"/> is used.
    /// </summary>
    public bool ExponentialBackoff { get; }

    /// <param name="count">Maximum retry attempts. Default: 3.</param>
    /// <param name="delayMs">Base delay between retries in milliseconds. Default: 100.</param>
    /// <param name="exponentialBackoff">Whether to use exponential backoff. Default: true.</param>
    public RetryAttribute(int count = 3, int delayMs = 100, bool exponentialBackoff = true) {
        Count = count;
        DelayMs = delayMs;
        ExponentialBackoff = exponentialBackoff;
    }
}