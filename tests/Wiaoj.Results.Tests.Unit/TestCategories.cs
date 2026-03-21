namespace Wiaoj.Results.Tests.Unit;

/// <summary>
/// Category constants for <c>[Trait("Category", ...)]</c>.
/// </summary>
internal static class Category {
    public const string Core = "Core";
    public const string Match = "Match";
    public const string Combinators = "Combinators";
    public const string Async = "Async";
    public const string Ensure = "Ensure";
    public const string Bridge = "Bridge";
    public const string ValueTask = "ValueTask";
    public const string Disposal = "Disposal";
    public const string Error = "Error";
}

/// <summary>
/// Shared factory helpers used across all test files.
/// </summary>
internal static class Fixtures {
    public static Error SomeError => Error.Failure("Test.Failure", "test failure");
    public static Error AnotherError => Error.Validation("Test.Validation", "test validation");
    public static Error NotFoundError => Error.NotFound("Test.NotFound", 42);

    public static Result<int> SuccessInt(int value = 42) {
        return value;
    }

    public static Result<string> SuccessString(string v = "ok") {
        return v;
    }

    public static Result<int> FailureInt() {
        return SomeError;
    }

    public static Result<string> FailureString() {
        return SomeError;
    }

    public static Task<Result<int>> SuccessIntTask(int value = 42) {
        return Task.FromResult(SuccessInt(value));
    }

    public static Task<Result<string>> SuccessStringTask(string v = "ok") {
        return Task.FromResult(SuccessString(v));
    }

    public static Task<Result<int>> FailureIntTask() {
        return Task.FromResult(FailureInt());
    }

    public static Task<Result<string>> FailureStringTask() {
        return Task.FromResult(FailureString());
    }
}