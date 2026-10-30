using System.Reflection;
using System.Text;
using Xunit;

namespace Wiaoj.Results.Tests.Unit;

/// <summary>
/// Keeps the awaitable combinator surface honest. Task and ValueTask shapes are compared after
/// normalising both awaitable kinds to one token, so an overload that exists for one kind and not the
/// other cannot be added unnoticed. The current asymmetry is not hidden: it is the <see cref="KnownValueTaskShapeGaps"/>
/// list, and that list is checked in both directions, so closing a gap fails until the gap is deleted.
/// </summary>
[Trait("Category", Category.Async)]
public sealed class ResultAwaitableParityTests {

    private static readonly string[] GuardedNames = [
        "MapAsync", "MapSuccessAsync", "BiMapAsync", "ThenAsync", "MatchAsync",
        "TapAsync", "TapErrorAsync", "IfSuccessAsync", "IfFailureAsync",
        "RecoverAsync", "EnsureAsync", "DoAsync"
    ];

    private const string TaskFamily = "ResultsExtensions";
    private const string ValueTaskFamily = "ResultsValueTaskExtensions";
    private const string StreamFamily = "ResultsAsyncEnumerableExtensions";

    public sealed class TheTaskAndValueTaskFamilies {

        [Fact]
        public void EveryValueTaskShapeAlsoExistsOnTheTaskFamily() {
            HashSet<string> taskShapes = [.. Shapes(TaskFamily)];

            string[] valueTaskOnly = Shapes(ValueTaskFamily).Where(s => !taskShapes.Contains(s)).ToArray();

            Assert.True(valueTaskOnly.Length == 0,
                $"ValueTask family exposes shapes the Task family does not:\n  {string.Join("\n  ", valueTaskOnly)}");
        }

        [Fact]
        public void TaskShapesMissingAValueTaskCounterpartAreExactlyTheKnownGaps() {
            HashSet<string> valueTaskShapes = [.. Shapes(ValueTaskFamily)];
            string[] actualGaps = [.. Shapes(TaskFamily).Where(s => !valueTaskShapes.Contains(s)).OrderBy(s => s, StringComparer.Ordinal)];

            string[] unexpected = [.. actualGaps.Where(g => !KnownValueTaskShapeGaps.Contains(g))];
            string[] stale = [.. KnownValueTaskShapeGaps.Where(g => !actualGaps.Contains(g))];

            Assert.True(unexpected.Length == 0 && stale.Length == 0,
                $"unexpected gaps (add the ValueTask overload, or justify it in the list):\n  {string.Join("\n  ", unexpected)}\n" +
                $"stale gaps (this shape now exists on both families, delete the entry):\n  {string.Join("\n  ", stale)}\n" +
                $"full current gap list:\n  {string.Join("\n  ", actualGaps)}");
        }
    }

    public sealed class TheStreamFamily {

        [Fact]
        public void IsExactlyTheReviewedSurface() {
            string[] actual = [.. Shapes(StreamFamily).OrderBy(s => s, StringComparer.Ordinal)];

            Assert.True(actual.SequenceEqual(ReviewedStreamSurface),
                $"stream surface moved.\n  actual:\n  {string.Join("\n  ", actual)}");
        }
    }

    /// <summary>
    /// Task-family combinator shapes with no ValueTask counterpart today. Sorted, ordinal.
    /// The first two groups are entire combinators; the rest are individual overloads.
    /// </summary>
    private static readonly string[] KnownValueTaskShapeGaps = [
        "DoAsync(AWAIT<Result<_>>,Func`2<CancellationToken,AWAIT>,CancellationToken)->AWAIT<Result<_>>",
        "DoAsync(Result<_>,Func`2<CancellationToken,AWAIT>,CancellationToken)->AWAIT<Result<_>>",
        "DoAsync(Result<_>,Func`3<_,CancellationToken,AWAIT>,CancellationToken)->AWAIT<Result<_>>",
        "EnsureAsync(AWAIT<Result<_>>,Func`2<_,AWAIT<Boolean>>,Func`2<_,AWAIT<Error>>)->AWAIT<Result<_>>",
        "EnsureAsync(Result<_>,Func`2<_,AWAIT<Boolean>>,Error)->AWAIT<Result<_>>",
        "EnsureAsync(Result<_>,Func`2<_,AWAIT<Boolean>>,Func`2<_,AWAIT<Error>>)->AWAIT<Result<_>>",
        "IfFailureAsync(AWAIT<Result<_>>,Func`3<IReadOnlyList<Error>,CancellationToken,AWAIT>,CancellationToken)->AWAIT<Result<_>>",
        "IfFailureAsync(Result<_>,Func`3<IReadOnlyList<Error>,CancellationToken,AWAIT>,CancellationToken)->AWAIT<Result<_>>",
        "IfSuccessAsync(AWAIT<Result<_>>,Func`3<_,CancellationToken,AWAIT>,CancellationToken)->AWAIT<Result<_>>",
        "MapAsync(AWAIT<Result<_>>,Func`2<_,Result<_>>)->AWAIT<Result<_>>",
        "MapAsync(AWAIT<Result<_>>,Func`3<_,CancellationToken,AWAIT<_>>,CancellationToken)->AWAIT<Result<_>>",
        "MapAsync(Result<_>,Func`2<_,AWAIT<_>>)->AWAIT<Result<_>>",
        "MapAsync(Result<_>,Func`3<_,CancellationToken,AWAIT<_>>,CancellationToken)->AWAIT<Result<_>>",
        "MapSuccessAsync(AWAIT<Result<_>>,CancellationToken)->AWAIT<Result<Success>>",
        "RecoverAsync(AWAIT<Result<_>>,Func`2<IReadOnlyList<Error>,AWAIT<_>>)->AWAIT<Result<_>>",
        "RecoverAsync(AWAIT<Result<_>>,Func`2<IReadOnlyList<Error>,_>)->AWAIT<Result<_>>",
        "RecoverAsync(Result<_>,Func`2<IReadOnlyList<Error>,AWAIT<_>>)->AWAIT<Result<_>>",
        "TapAsync(AWAIT<Result<_>>,Func`2<CancellationToken,AWAIT>,CancellationToken)->AWAIT<Result<_>>",
        "TapAsync(Result<_>,Func`2<CancellationToken,AWAIT>,CancellationToken)->AWAIT<Result<_>>",
        "TapAsync(Result<_>,Func`3<_,CancellationToken,AWAIT>,CancellationToken)->AWAIT<Result<_>>",
        "TapErrorAsync(AWAIT<Result<_>>,Func`3<IReadOnlyList<Error>,CancellationToken,AWAIT>,CancellationToken)->AWAIT<Result<_>>",
        "TapErrorAsync(Result<_>,Func`3<IReadOnlyList<Error>,CancellationToken,AWAIT>,CancellationToken)->AWAIT<Result<_>>",
        "ThenAsync(AWAIT<Result<_>>,Func`2<_,Result<_>>)->AWAIT<Result<_>>"
    ];

    private static readonly string[] ReviewedStreamSurface = [
        "MapAsync(STREAM<Result<_>>,Func`2<_,AWAIT<_>>,CancellationToken)->STREAM<Result<_>>",
        "MapAsync(STREAM<Result<_>>,Func`2<_,_>,CancellationToken)->STREAM<Result<_>>",
        "ThenAsync(STREAM<Result<_>>,Func`2<_,AWAIT<Result<_>>>,CancellationToken)->STREAM<Result<_>>"
    ];

    // A shape key is name(params)->return. Task<T> and ValueTask<T> both render as AWAIT<T> and
    // IAsyncEnumerable<T> as STREAM<T>, which is what makes the two families comparable; a method's own
    // generic parameters render as _ because their names differ per family, while closed types keep
    // their simple name. Delegates keep their arity so Func<T,R> and Func<T,CancellationToken,R> stay
    // distinct shapes.
    private static IEnumerable<string> Shapes(string family) =>
        Methods(family).Select(Key);

    private static string Key(MethodInfo method) {
        StringBuilder sb = new(method.Name);
        sb.Append('(');
        ParameterInfo[] parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++) {
            if (i > 0) {
                sb.Append(',');
            }

            sb.Append(Normalize(parameters[i].ParameterType));
        }

        return sb.Append(")->").Append(Normalize(method.ReturnType)).ToString();
    }

    private static string Normalize(Type type) {
        if (type.IsArray) {
            return Normalize(type.GetElementType()!) + "[]";
        }

        if (type.IsGenericParameter) {
            return "_";
        }

        if (!type.IsGenericType) {
            return Token(type.Name);
        }

        Type definition = type.GetGenericTypeDefinition();
        string name = definition.Name;
        int backtick = name.IndexOf('`');
        string bare = backtick < 0 ? name : name[..backtick];
        Type[] arguments = type.GetGenericArguments();
        string normalisedArguments = string.Join(",", arguments.Select(Normalize));

        return bare switch {
            "Task" or "ValueTask" => $"AWAIT<{normalisedArguments}>",
            "IAsyncEnumerable" => $"STREAM<{normalisedArguments}>",
            "Func" or "Action" => $"{bare}`{arguments.Length}<{normalisedArguments}>",
            _ => $"{Token(bare)}<{normalisedArguments}>"
        };
    }

    private static string Token(string name) => name switch {
        "Task" or "ValueTask" => "AWAIT",
        "IAsyncEnumerable" or "IAsyncEnumerator" => "STREAM",
        _ => name
    };

    private static IEnumerable<MethodInfo> Methods(string family) {
        Type type = typeof(Result<>).Assembly.GetType($"Wiaoj.Results.{family}")
            ?? throw new InvalidOperationException($"{family} not found");

        return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => GuardedNames.Contains(m.Name));
    }
}
