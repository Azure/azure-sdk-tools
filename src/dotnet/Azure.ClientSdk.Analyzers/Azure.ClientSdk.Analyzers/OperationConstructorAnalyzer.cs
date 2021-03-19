// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OperationConstructorAnalyzer : OperationAnalyzerBase
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Descriptors.AZC0005,
        });


        public override void AnalyzeCore(ISymbolAnalysisContext context)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (!type.Constructors.Any(c => (c.DeclaredAccessibility == Accessibility.Protected || c.DeclaredAccessibility == Accessibility.Public) && c.Parameters.Length == 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0005, type.Locations.First()), type);
            }
        }
    }
}