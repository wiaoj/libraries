using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Wiaoj.Identifiers.Generators;

/// <summary>
/// Generates the members of every <c>readonly partial record struct</c> marked with <c>[Identifier("prefix")]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class IdentifierGenerator : IIncrementalGenerator {
    private const string AttributeName = "Wiaoj.Identifiers.IdentifierAttribute";
    private const int MaxPrefixLength = 32;

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        IncrementalValuesProvider<IdentifierTarget> targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (context, cancellationToken) => Inspect(context, cancellationToken));

        context.RegisterSourceOutput(targets.Collect(), static (output, all) => Emit(output, all));
    }

    private static IdentifierTarget Inspect(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken) {
        INamedTypeSymbol symbol = (INamedTypeSymbol)context.TargetSymbol;
        AttributeData attribute = context.Attributes[0];
        string? prefix = attribute.ConstructorArguments.Length == 1 ? attribute.ConstructorArguments[0].Value as string : null;
        LocationInfo location = LocationInfo.From(context.TargetNode.GetLocation());

        List<DiagnosticInfo> diagnostics = [];

        if(!IsValidPrefix(prefix)) {
            diagnostics.Add(new DiagnosticInfo(Diagnostics.InvalidPrefix, location, symbol.Name, prefix ?? "null"));
        }

        if(symbol.ContainingType is not null || symbol.TypeParameters.Length > 0) {
            diagnostics.Add(new DiagnosticInfo(Diagnostics.NestedOrGeneric, location, symbol.Name));
        }

        if(!IsReadonlyPartialRecordStruct(symbol, cancellationToken)) {
            diagnostics.Add(new DiagnosticInfo(Diagnostics.WrongShape, location, symbol.Name));
        }

        string ns = symbol.ContainingNamespace.IsGlobalNamespace ? "" : symbol.ContainingNamespace.ToDisplayString();
        string accessibility = symbol.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";

        return new IdentifierTarget(ns, symbol.Name, accessibility, prefix ?? "", location, new EquatableArray<DiagnosticInfo>([.. diagnostics]));
    }

    /// <summary>Every declaration must be a <c>readonly partial record struct</c> with no parameter list of its own.</summary>
    private static bool IsReadonlyPartialRecordStruct(INamedTypeSymbol symbol, CancellationToken cancellationToken) {
        foreach(SyntaxReference reference in symbol.DeclaringSyntaxReferences) {
            if(reference.GetSyntax(cancellationToken) is not RecordDeclarationSyntax record
               || !record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
               || !record.Modifiers.Any(SyntaxKind.PartialKeyword)
               || !record.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
               || record.ParameterList is not null) {
                return false;
            }
        }

        return symbol.DeclaringSyntaxReferences.Length > 0;
    }

    /// <summary>The same rule as <c>IdCodec.IsValidPrefix</c>, which the generator cannot reference.</summary>
    internal static bool IsValidPrefix(string? prefix) {
        if(string.IsNullOrEmpty(prefix) || prefix!.Length > MaxPrefixLength || prefix[0] is < 'a' or > 'z' || prefix[prefix.Length - 1] == '_') {
            return false;
        }

        for(int i = 1; i < prefix.Length; i++) {
            char c = prefix[i];
            bool allowed = c is (>= 'a' and <= 'z') or (>= '0' and <= '9') || (c == '_' && prefix[i - 1] != '_');
            if(!allowed) {
                return false;
            }
        }

        return true;
    }

    private static void Emit(SourceProductionContext output, ImmutableArray<IdentifierTarget> targets) {
        HashSet<string> seen = [];
        foreach(IGrouping<string, IdentifierTarget> group in targets.Where(t => IsValidPrefix(t.Prefix)).GroupBy(t => t.Prefix)) {
            if(group.Count() > 1) {
                string names = string.Join(", ", group.Select(t => t.FullName).OrderBy(n => n, System.StringComparer.Ordinal));
                foreach(IdentifierTarget target in group) {
                    output.ReportDiagnostic(Diagnostic.Create(Diagnostics.DuplicatePrefix, target.Location.ToLocation(), target.Prefix, names));
                }
            }
        }

        foreach(IdentifierTarget target in targets) {
            foreach(DiagnosticInfo diagnostic in target.Diagnostics) {
                output.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            // A struct the rules refuse gets no members: the diagnostic is the one error, not a cascade from invalid code.
            if(target.Diagnostics.Count > 0 || !seen.Add(target.FullName)) {
                continue;
            }

            output.AddSource($"{target.FullName}.Identifier.g.cs", SourceText.From(IdentifierEmitter.Emit(target), System.Text.Encoding.UTF8));
        }
    }
}
