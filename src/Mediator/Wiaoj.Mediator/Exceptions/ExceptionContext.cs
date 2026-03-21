using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Mediator;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// Holds the recovery state for an exception caught in the pipeline.
/// <para>
/// Call <see cref="SetHandled"/> to swallow the exception and return a fallback value.
/// If <see cref="SetHandled"/> is never called, the exception is re-thrown after the handler returns.
/// </para>
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class ExceptionContext<TResponse> {

    /// <summary>Whether the exception was marked as handled (swallowed).</summary>
    public bool IsHandled { get; private set; }

    /// <summary>
    /// The fallback response. Only valid when <see cref="IsHandled"/> is <c>true</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed before calling <see cref="SetHandled"/>.</exception>
    [AllowNull]
    public TResponse Result { get => this.IsHandled
        ? field!
        : throw new InvalidOperationException(
            $"Exception is not handled. Call {nameof(SetHandled)}(response) before accessing {nameof(this.Result)}."); private set;
    }

    /// <summary>
    /// Marks the exception as handled and provides a fallback response.
    /// The exception will NOT be re-thrown after the handler returns.
    /// </summary>
    /// <param name="result">The fallback response to return to the caller.</param>
    public void SetHandled(TResponse result) {
        this.Result = result;
        this.IsHandled = true;
    }
}