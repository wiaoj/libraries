namespace Wiaoj.Mediator.Behaviors;
/// <summary>
/// Enforces a maximum execution time for a request.
/// <para>
/// If the handler does not complete within the specified duration,
/// a <see cref="TimeoutException"/> is thrown and the caller's <see cref="CancellationToken"/> is signalled.
/// </para>
/// </summary>
/// <example>
/// <code>
/// [Timeout(milliseconds: 500)]
/// public record GetProductQuery(Guid Id) : IQuery&lt;ProductDto&gt;;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TimeoutAttribute : Attribute {
    /// <summary>Maximum allowed execution time in milliseconds.</summary>
    public int Milliseconds { get; }

    /// <param name="milliseconds">Timeout duration in milliseconds.</param>
    public TimeoutAttribute(int milliseconds) {
        Preca.ThrowIfNegativeOrZero(milliseconds);
        this.Milliseconds = milliseconds;
    }
}