// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TypeNameAnalyzer : SymbolAnalyzerBase
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.AZC0012);
        public override SymbolKind[] SymbolKinds { get; } = { SymbolKind.NamedType };

        public override void Analyze(ISymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Account for an `I` prefix in the interfaces.
            var minimumWordCount = namedTypeSymbol.TypeKind == TypeKind.Interface ? 2 : 1;
            if (namedTypeSymbol.DeclaredAccessibility == Accessibility.Public &&
                namedTypeSymbol.ContainingType == null &&
                CountWords(namedTypeSymbol.Name) <= minimumWordCount)
            {
                foreach (var location in namedTypeSymbol.Locations)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0012, location, context.Symbol), context.Symbol);
                }
            }
        }

        private int CountWords(string name)
        {
            int i = 0;
            foreach (var c in name)
            {
                if (char.IsUpper(c))
                {
                    i++;
                }
            }

            return i;
        }
    }
}