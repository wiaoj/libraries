using Wiaoj.Results;
using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Match)]
public sealed class ResultMatchTests {

    // ── Match ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Match_WhenSuccess_CallsOnValue() {
        Result<int> result = 42;
        string output = result.Match(
            onValue: v => $"value:{v}",
            onError: _ => "error");
        Assert.Equal("value:42", output);
    }

    [Fact]
    public void Match_WhenError_CallsOnError() {
        Result<int> result = SomeError;
        string output = result.Match(
            onValue: _ => "value",
            onError: e => $"error:{e.Count}");
        Assert.Equal("error:1", output);
    }

    [Fact]
    public void Match_WhenSuccess_DoesNotCallOnError() {
        Result<int> result = 42;
        bool errorCalled = false;
        result.Match(
            onValue: v => v,
            onError: e => { errorCalled = true; return 0; });
        Assert.False(errorCalled);
    }

    [Fact]
    public void Match_WhenError_DoesNotCallOnValue() {
        Result<int> result = SomeError;
        bool valueCalled = false;
        result.Match(
            onValue: v => { valueCalled = true; return ""; },
            onError: _ => "error");
        Assert.False(valueCalled);
    }

    [Fact]
    public void Match_ReturnsCorrectType() {
        Result<int> result = 5;
        int doubled = result.Match(
            onValue: v => v * 2,
            onError: _ => -1);
        Assert.Equal(10, doubled);
    }

    [Fact]
    public void Match_WhenMultipleErrors_PassesAllErrorsToOnError() {
        List<Error> errors = [SomeError, AnotherError];
        Result<int> result = errors;
        int errorCount = result.Match(
            onValue: _ => 0,
            onError: e => e.Count);
        Assert.Equal(2, errorCount);
    }

    // ── Switch ────────────────────────────────────────────────────────────────

    [Fact]
    public void Switch_WhenSuccess_CallsOnValue() {
        Result<int> result = 42;
        int captured = 0;
        result.Switch(
            onValue: v => { captured = v; },
            onError: _ => { captured = -1; });
        Assert.Equal(42, captured);
    }

    [Fact]
    public void Switch_WhenError_CallsOnError() {
        Result<int> result = SomeError;
        bool errorCalled = false;
        result.Switch(
            onValue: _ => { },
            onError: _ => { errorCalled = true; });
        Assert.True(errorCalled);
    }

    [Fact]
    public void Switch_WhenSuccess_DoesNotCallOnError() {
        Result<int> result = 42;
        bool errorCalled = false;
        result.Switch(
            onValue: _ => { },
            onError: _ => { errorCalled = true; });
        Assert.False(errorCalled);
    }

    [Fact]
    public void Switch_WhenError_DoesNotCallOnValue() {
        Result<int> result = SomeError;
        bool valueCalled = false;
        result.Switch(
            onValue: _ => { valueCalled = true; },
            onError: _ => { });
        Assert.False(valueCalled);
    }
}