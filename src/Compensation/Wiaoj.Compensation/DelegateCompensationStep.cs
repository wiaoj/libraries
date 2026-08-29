using Wiaoj.Preconditions;

namespace Wiaoj.Compensation;

/// <summary>
/// Internal adapter that wraps delegate actions into an <see cref="ICompensationStep{TContext}"/>.
/// </summary>
/// <typeparam name="TContext">The shared context type.</typeparam>
internal sealed class DelegateCompensationStep<TContext> : ICompensationStep<TContext> {
    private readonly Func<TContext, CancellationToken, ValueTask> _execute;
    private readonly Func<TContext, CancellationToken, ValueTask>? _compensate;

    /// <summary>
    /// Gets the human-readable identifier of this step.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateCompensationStep{TContext}"/> class.
    /// </summary>
    /// <param name="name">The name of the step.</param>
    /// <param name="execute">The forward execution delegate.</param>
    /// <param name="compensate">The optional backward compensation delegate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="execute"/> is null.</exception>
    public DelegateCompensationStep(
        string name,
        Func<TContext, CancellationToken, ValueTask> execute,
        Func<TContext, CancellationToken, ValueTask>? compensate = null) {
        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNull(execute);

        this.Name = name;
        this._execute = execute;
        this._compensate = compensate;
    }

    /// <inheritdoc />
    public ValueTask ExecuteAsync(TContext context, CancellationToken cancellationToken) {
        return this._execute(context, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask CompensateAsync(TContext context, CancellationToken cancellationToken) {
        return this._compensate is not null
            ? this._compensate(context, cancellationToken)
            : ValueTask.CompletedTask;
    }
}