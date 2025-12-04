namespace Wiaoj.Preconditions.Extensions;

[DebuggerNonUserCode, DebuggerStepThrough]
public static class ThrowIfExtensions {
    /// <summary>
    /// Validates that <paramref name="argument"/> is of type <typeparamref name="TDerived"/>.
    /// Throws <see cref="PrecaArgumentException"/> if not.
    /// </summary>
    /// <typeparam name="TBase">The base type of the argument.</typeparam>
    /// <typeparam name="TDerived">The expected derived type. Must inherit from <typeparamref name="TBase"/>.</typeparam>
    /// <param name="_">Dummy extension parameter for fluent syntax.</param>
    /// <param name="argument">The object to validate and cast.</param>
    /// <param name="paramName">Automatically captured parameter name.</param>
    /// <returns>The argument cast to <typeparamref name="TDerived"/> if validation succeeds.</returns>
    [DebuggerStepThrough, DebuggerHidden, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TDerived ThrowIfNotType<TBase, TDerived>(this PrecaExtensions _,
                                                           [NotNull] TBase? argument,
                                                           [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where TDerived : TBase {
        if (argument is not TDerived tDerived) {
            Thrower.ThrowPrecaArgumentException($"Invalid builder configuration. Expected type {typeof(TDerived).Name}.", paramName);

            // This line will never be reached; it exists only to satisfy the compiler
            return default;
        }

        return tDerived;
    }
}