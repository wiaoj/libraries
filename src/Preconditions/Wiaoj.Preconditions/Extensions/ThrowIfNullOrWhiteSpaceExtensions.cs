namespace Wiaoj.Preconditions.Extensions;

[DebuggerNonUserCode, DebuggerStepThrough]
public static class ThrowIfNullOrWhiteSpaceExtensions {
    /// <summary>
    /// Validates that the specified string is not null, empty, or consists only of whitespace characters.
    /// </summary>
    /// <param name="_"></param>
    /// <param name="argument">The string to validate.</param>
    /// <param name="paramName">The name of the parameter being validated. This parameter is automatically populated by the compiler.</param>
    /// <exception cref="PrecaArgumentNullException">Thrown when <paramref name="argument"/> is null. Inherits from <see cref="ArgumentNullException"/>.</exception>
    /// <exception cref="PrecaArgumentException">Thrown when <paramref name="argument"/> is empty or consists only of whitespace. Inherits from <see cref="ArgumentException"/>.</exception>
    /// <remarks>
    /// Use this method to validate string parameters that must contain meaningful content beyond whitespace.
    /// Validates against spaces, tabs, newlines, and other Unicode whitespace characters.
    /// </remarks>
    [DebuggerStepThrough, DebuggerHidden, StackTraceHidden]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ThrowIfNullOrWhiteSpace(this PrecaExtensions _,
                                                 [NotNull] string? argument,
                                                 [CallerArgumentExpression(nameof(argument))] string? paramName = null) {
        if (string.IsNullOrWhiteSpace(argument)) {
            if (argument is null) {
                Thrower.ThrowPrecaArgumentNullException(paramName);
            }
            else if (argument.Length is Preca.Constants.ZeroInt32) {
                Thrower.ThrowPrecaArgumentException(PrecaMessages.Text.ValueCannotBeNullOrEmpty, paramName);
            }

            Thrower.ThrowPrecaArgumentException(PrecaMessages.Text.ValueCannotBeNullOrWhiteSpace, paramName);
        }

        return argument;
    }
}