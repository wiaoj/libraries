namespace Wiaoj.Results.Tests.Unit;

internal static class Category {
    public const string StateAndInvariants = "StateAndInvariants";
    public const string Error = "Error";
    public const string Match = "Match";
    public const string Combinators = "Combinators";
    public const string Async = "Async";
    public const string Ensure = "Ensure";
    public const string Bridge = "Bridge";
    public const string Collection = "Collection";
    public const string Combine = "Combine";
    public const string ValueTask = "ValueTask";
    public const string Disposal = "Disposal";
    public const string Try = "Try"; 
    
    public const string AsyncEnumerable = "AsyncEnumerable"; 
    public const string StressAndChaos = "StressAndChaos"; 
    public const string Serialization = "Serialization";
}

internal static class Fixtures {
    public static Error SomeError => Error.Failure("Test.Failure", "A test failure occurred.");
    public static Error AnotherError => Error.Validation("Test.Validation", "A test validation error occurred.");
    public static Error NotFoundError => Error.NotFound("Test.NotFound", 42);

    public static Result<int> SuccessInt(int value = 42) {
        return value;
    }

    public static Result<string> SuccessString(string value = "ok") {
        return value;
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

    public static Task<Result<string>> SuccessStringTask(string value = "ok") {
        return Task.FromResult(SuccessString(value));
    }

    public static Task<Result<int>> FailureIntTask() {
        return Task.FromResult(FailureInt());
    }

    public static Task<Result<string>> FailureStringTask() {
        return Task.FromResult(FailureString());
    }
}