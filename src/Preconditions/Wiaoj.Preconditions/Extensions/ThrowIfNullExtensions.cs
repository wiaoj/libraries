using Wiaoj.Preconditions.Extensions;

namespace Wiaoj.Preconditions.Extensions;

[DebuggerNonUserCode, DebuggerStepThrough]
public static class ThrowIfNullExtensions {
    /// <summary>
    /// Validates that the specified argument is not null.
    /// </summary>
    /// <param name="_"></param>
    /// <param name="argument">The argument to validate. Must not be null.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="argument"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <remarks>
    /// This method provides high-performance null checking with aggressive inlining.
    /// The parameter name is automatically captured using CallerArgumentExpressionAttribute.
    /// </remarks>
    [DebuggerStepThrough, DebuggerHidden, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ThrowIfNull<T>(this PrecaExtensions _,
                                   [NotNull] T? argument,
                                   [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        Preca.ThrowIfNull(argument, paramName);
        return argument;
    }
}