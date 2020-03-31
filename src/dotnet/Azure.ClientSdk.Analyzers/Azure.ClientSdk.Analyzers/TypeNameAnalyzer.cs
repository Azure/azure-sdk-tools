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

            if (namedTypeSymbol.DeclaredAccessibility == Accessibility.Public &&
                IsSingleWord(namedTypeSymbol.Name))
            {
                foreach (var location in namedTypeSymbol.Locations)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0012, location, context.Symbol), context.Symbol);
                }
            }
        }

        private bool IsSingleWord(string name)
        {
            int i = 0;
            foreach (var c in name)
            {
                if (char.IsUpper(c))
                {
                    i++;
                }

                if (i > 1)
                {
                    return false;
                }
            }

            return true;
        }
    }
}