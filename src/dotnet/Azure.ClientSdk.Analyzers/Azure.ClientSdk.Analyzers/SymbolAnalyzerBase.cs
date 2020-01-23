// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    public abstract class SymbolAnalyzerBase : DiagnosticAnalyzer
    {
        public abstract SymbolKind[] SymbolKinds { get; }
        public abstract void Analyze(ISymbolAnalysisContext context);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(c => Analyze(new RoslynSymbolAnalysisContext(c)), SymbolKinds);
        }

        class RoslynSymbolAnalysisContext : ISymbolAnalysisContext
        {
            private readonly SymbolAnalysisContext _context;

            public RoslynSymbolAnalysisContext(SymbolAnalysisContext context)
            {
                _context = context;
            }

            public ISymbol Symbol => _context.Symbol;
            public void ReportDiagnostic(Diagnostic diagnostic, ISymbol symbol)
            {
                _context.ReportDiagnostic(diagnostic);
            }
        }
    }
}