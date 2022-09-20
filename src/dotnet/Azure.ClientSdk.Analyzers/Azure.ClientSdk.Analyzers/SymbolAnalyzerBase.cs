// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    public abstract class SymbolAnalyzerBase : DiagnosticAnalyzer
    {
        private const string ClientOptionsSuffix = "ClientOptions";
        private const string ClientsOptionsSuffix = "ClientsOptions";

        public abstract SymbolKind[] SymbolKinds { get; }
        public abstract void Analyze(ISymbolAnalysisContext context);

        protected INamedTypeSymbol ClientOptionsType { get; private set; }

        public sealed override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(CompilationStart);
            context.RegisterSymbolAction(c => Analyze(new RoslynSymbolAnalysisContext(c)), SymbolKinds);
        }

#pragma warning disable RS1012 // Start action has no registered actions.
        protected virtual void CompilationStart(CompilationStartAnalysisContext context)
        {
            ClientOptionsType = context.Compilation.GetTypeByMetadataName("Azure.Core.ClientOptions");
        }
#pragma warning restore RS1012 // Start action has no registered actions.

        protected bool IsPublicApi(ISymbol symbol)
        {
            if (symbol.ContainingSymbol != null && !IsPublicApi(symbol.ContainingSymbol))
            {
                return false;
            }

            return symbol.DeclaredAccessibility == Accessibility.NotApplicable ||
                   symbol.DeclaredAccessibility == Accessibility.Public ||
                   symbol.DeclaredAccessibility == Accessibility.Protected;
        }

        protected bool IsClientOptionsType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
            
            ITypeSymbol baseType = typeSymbol.BaseType;
            while (baseType != null) 
            {
                if (SymbolEqualityComparer.Default.Equals(baseType, ClientOptionsType))
                {
                    return typeSymbol.Name.EndsWith(ClientOptionsSuffix) || typeSymbol.Name.EndsWith(ClientsOptionsSuffix);
                }

                baseType = baseType.BaseType;
            }

            return false;
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