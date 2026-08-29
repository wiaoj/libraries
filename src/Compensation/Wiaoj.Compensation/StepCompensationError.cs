namespace Wiaoj.Compensation;

/// <summary>
/// Detailed error information recorded when a specific step's rollback fails.
/// </summary>
/// <param name="StepName">The name of the step whose compensation failed.</param>
/// <param name="ErrorMessage">The message of the exception thrown during rollback.</param>
/// <param name="ExceptionType">The type name of the exception thrown during rollback.</param>
public readonly record struct StepCompensationError(
    string StepName,
    string ErrorMessage,
    string ExceptionType
);