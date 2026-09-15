using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Wiaoj.Identifiers.Generators;

namespace Wiaoj.Identifiers.Tests.Unit;

/// <summary>
/// The generator refuses declarations it cannot generate correctly with one clear error, and generates code that compiles
/// for every valid one.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Identifiers")]
[Trait("Component", "Generator")]
public sealed class GeneratorDiagnosticsTests {
    private static readonly ImmutableArray<MetadataReference> References = [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)),
        MetadataReference.CreateFromFile(typeof(IdCodec).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Primitives.Snowflake.SnowflakeId).Assembly.Location)
    ];

    private static (ImmutableArray<Diagnostic> GeneratorDiagnostics, ImmutableArray<Diagnostic> CompilationErrors, int GeneratedFiles) Run(string source) {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new IdentifierGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> diagnostics, TestContext.Current.CancellationToken);

        ImmutableArray<Diagnostic> errors = [.. output.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error)];
        return (diagnostics, errors, driver.GetRunResult().GeneratedTrees.Length);
    }

    private static string[] Ids(ImmutableArray<Diagnostic> diagnostics) => [.. diagnostics.Select(d => d.Id)];

    [Fact]
    public void Should_Generate_Code_That_Compiles_For_Valid_Identifiers() {
        (ImmutableArray<Diagnostic> diagnostics, ImmutableArray<Diagnostic> errors, int files) = Run("""
            using Wiaoj.Identifiers;
            namespace Shop;
            [Identifier("usr")] public readonly partial record struct UserId;
            [Identifier("api_key")] internal readonly partial record struct ApiKeyId;
            public readonly partial record struct TwoParts;
            [Identifier("two")] public readonly partial record struct TwoParts { public string Describe() => Prefix; }
            """);

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        Assert.Equal(3, files);
    }

    [Fact]
    public void Should_Generate_For_An_Identifier_In_The_Global_Namespace() {
        (ImmutableArray<Diagnostic> diagnostics, ImmutableArray<Diagnostic> errors, int files) = Run("""
            [Wiaoj.Identifiers.Identifier("g")] public readonly partial record struct GlobalId;
            """);

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        Assert.Equal(1, files);
    }

    [Theory]
    [InlineData("Usr")]
    [InlineData("1usr")]
    [InlineData("usr_")]
    [InlineData("api__key")]
    [InlineData("api-key")]
    [InlineData("")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")]
    public void Should_Refuse_An_Invalid_Prefix(string prefix) {
        (ImmutableArray<Diagnostic> diagnostics, ImmutableArray<Diagnostic> errors, int files) = Run($$"""
            [Wiaoj.Identifiers.Identifier("{{prefix}}")] public readonly partial record struct BadId;
            """);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("WIAOJID001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains($"'{prefix}'", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Equal(0, files);
        Assert.Empty(errors);
    }

    [Fact]
    public void Should_Refuse_A_Null_Prefix() {
        (ImmutableArray<Diagnostic> diagnostics, _, int files) = Run("""
            [Wiaoj.Identifiers.Identifier(null!)] public readonly partial record struct BadId;
            """);

        Assert.Equal(["WIAOJID001"], Ids(diagnostics));
        Assert.Equal(0, files);
    }

    [Theory]
    [InlineData("public partial record struct BadId;")]
    [InlineData("public readonly record struct BadId;")]
    [InlineData("public readonly partial struct BadId;")]
    [InlineData("public partial record class BadId;")]
    [InlineData("public partial class BadId;")]
    [InlineData("public readonly partial record struct BadId(long Other);")]
    public void Should_Refuse_A_Declaration_That_Is_Not_A_Readonly_Partial_Record_Struct(string declaration) {
        (ImmutableArray<Diagnostic> diagnostics, _, int files) = Run($$"""
            [Wiaoj.Identifiers.Identifier("bad")] {{declaration}}
            """);

        Assert.Contains("WIAOJID002", Ids(diagnostics));
        Assert.Equal(0, files);
    }

    [Fact]
    public void Should_Refuse_A_Prefix_Used_By_Two_Identifiers_On_Both() {
        (ImmutableArray<Diagnostic> diagnostics, _, _) = Run("""
            using Wiaoj.Identifiers;
            namespace A { [Identifier("usr")] public readonly partial record struct UserId; }
            namespace B { [Identifier("usr")] public readonly partial record struct CustomerId; }
            [Identifier("org")] public readonly partial record struct OrgId;
            """);

        Assert.Equal(["WIAOJID003", "WIAOJID003"], Ids(diagnostics));
        Assert.All(diagnostics, d => Assert.Contains("A.UserId, B.CustomerId", d.GetMessage(), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("public static class Outer { [Wiaoj.Identifiers.Identifier(\"inner\")] public readonly partial record struct InnerId; }")]
    [InlineData("[Wiaoj.Identifiers.Identifier(\"gen\")] public readonly partial record struct GenericId<T>;")]
    public void Should_Refuse_A_Nested_Or_Generic_Identifier(string source) {
        (ImmutableArray<Diagnostic> diagnostics, _, int files) = Run(source);

        Assert.Contains("WIAOJID004", Ids(diagnostics));
        Assert.Equal(0, files);
    }
}
