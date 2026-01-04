using System.ComponentModel;
using Wiaoj.Preconditions.Exceptions;

namespace Wiaoj.Preconditions;
/// <summary>
/// Centralized exception throwing utility for maximum performance and clean stack traces.
/// All guard method exceptions are routed through this class for consistency and optimization.
/// </summary>
[DebuggerStepThrough, DebuggerNonUserCode, StackTraceHidden, EditorBrowsable(EditorBrowsableState.Never)]
internal static class Thrower {
    /// <summary>
    /// Throws a <see cref="PrecaArgumentNullException"/> with the specified parameter name.
    /// </summary>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <exception cref="PrecaArgumentNullException">Always thrown. Inherits from ArgumentNullException.</exception>
    [DebuggerHidden, StackTraceHidden, DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ThrowPrecaArgumentNullException(string? paramName) {
        throw new PrecaArgumentNullException(paramName);
    }

    [DebuggerHidden, StackTraceHidden, DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ThrowPrecaArgumentNullException(string? paramName, string? message) {
        throw new PrecaArgumentNullException(paramName, message);
    }

    /// <summary>
    /// Throws a <see cref="PrecaArgumentException"/> with the specified message and parameter name.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <exception cref="PrecaArgumentException">Always thrown. Inherits from ArgumentException.</exception>
    [DebuggerHidden, StackTraceHidden, DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ThrowPrecaArgumentException([NotNull] string message, string? paramName) {
        throw new PrecaArgumentException(message, paramName);
    }

    /// <summary>
    /// Throws a <see cref="PrecaArgumentOutOfRangeException"/> with the specified parameter name, actual value, and message.
    /// </summary>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="actualValue">The value of the argument that causes this exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Always thrown. Inherits from ArgumentOutOfRangeException.</exception>
    [DebuggerHidden, StackTraceHidden, DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ThrowPrecaArgumentOutOfRangeException(string? paramName, object? actualValue, [NotNull] string message) {
        throw new PrecaArgumentOutOfRangeException(paramName, actualValue, message);
    }

    /// <summary>
    /// Throws a <see cref="PrecaArgumentOutOfRangeException"/> with the specified parameter name and message.
    /// </summary>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <exception cref="PrecaArgumentOutOfRangeException">Always thrown. Inherits from ArgumentOutOfRangeException.</exception>
    [DebuggerHidden, StackTraceHidden, DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ThrowPrecaArgumentOutOfRangeException(string? paramName, [NotNull] string message) {
        throw new PrecaArgumentOutOfRangeException(paramName, message);
    }

    /// <summary>
    /// Throws a <see cref="PrecaArgumentValueException"/> with the specified parameter name, actual value, and message.
    /// </summary>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="actualValue">The value of the argument that causes this exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <exception cref="PrecaArgumentValueException">Always thrown. Inherits from ArgumentOutOfRangeException.</exception>
    [DebuggerHidden, StackTraceHidden, DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ThrowPrecaArgumentValueException(string? paramName, object? actualValue, [NotNull] string message) {
        throw new PrecaArgumentValueException(paramName, actualValue, message);
    }

    /// <summary>
    /// Throws a <see cref="PrecaArgumentValueException"/> with the specified parameter name and message.
    /// </summary>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <exception cref="PrecaArgumentValueException">Always thrown. Inherits from ArgumentOutOfRangeException.</exception>
    [DebuggerHidden, StackTraceHidden, DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ThrowPrecaArgumentValueException(string? paramName, [NotNull] string message) {
        throw new PrecaArgumentValueException(paramName, message);
    }

    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowPrecaInvalidTypeException<TExpected>(object? argument, string? paramName) {
        string expectedTypeName = typeof(TExpected).Name;
        string actualTypeName = argument?.GetType().Name ?? "null";

        throw new PrecaInvalidTypeException(paramName, expectedTypeName, actualTypeName);
    }

    /// <summary>
    /// Creates and throws an exception of the specified type using the parameterless constructor.
    /// </summary>
    /// <typeparam name="TException">The type of exception to throw. Must have a parameterless constructor.</typeparam>
    /// <exception cref="Exception">Always thrown. The specific exception type is determined by the generic parameter.</exception>
    [DebuggerHidden, StackTraceHidden, DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ThrowException<TException>() where TException : Exception, new() {
        throw new TException();
    }

    [DebuggerHidden, StackTraceHidden, DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ThrowFromFactory<TException>([NotNull] Func<TException> exceptionFactory) where TException : notnull, Exception {
        TException? exception = exceptionFactory();
        if (exception is null) {
            Thrower.ThrowPrecaArgumentNullException(nameof(exceptionFactory), PrecaMessages.Core.ExceptionFactoryReturnedNull);
        }

        throw exception;
    }

    [DebuggerHidden, StackTraceHidden, DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ThrowFromFactory<TState, TException>(
        [NotNull] Func<TState, TException> exceptionFactory,
        [NotNull] TState state) where TException : notnull, Exception {
        TException? exception = exceptionFactory(state);

        if (exception is null) {
            Thrower.ThrowPrecaArgumentNullException(nameof(exceptionFactory), PrecaMessages.Core.ExceptionFactoryReturnedNull);
        }

        throw exception;
    }
}