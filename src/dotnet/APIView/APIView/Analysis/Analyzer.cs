// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ApiView;
using Azure.ClientSdk.Analyzers;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace APIView.Analysis
{
    public class Analyzer : SymbolVisitor
    {
        public List<CodeDiagnostic> Results { get; } = new List<CodeDiagnostic>();

        private readonly List<SymbolAnalyzerBase> _analyzers = new List<SymbolAnalyzerBase>();
        private readonly List<SdkAnalyzerAdapter> _sdkAnalyzers = new List<SdkAnalyzerAdapter>();

        public Analyzer()
        {
            // Analyzers from Azure.ClientSdk.Analyzers (uses ISymbolAnalysisContext)
            _analyzers.Add(new ClientMethodsAnalyzer());
            _analyzers.Add(new ClientConstructorAnalyzer());
            _analyzers.Add(new ClientOptionsAnalyzer());
            _analyzers.Add(new BannedAssembliesAnalyzer());

            // Analyzers from Azure.SdkAnalyzers (uses SymbolAnalysisContext)
            _sdkAnalyzers.Add(new SdkAnalyzerAdapter(new Azure.SdkAnalyzers.TypeNameAnalyzer()));
        }

        public override void VisitAssembly(IAssemblySymbol symbol)
        {
            if (symbol.Name.StartsWith("Azure"))
            {
                Visit(symbol.GlobalNamespace);
            }
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            foreach (var namespaceOrTypeSymbol in symbol.GetMembers())
            {
                Visit(namespaceOrTypeSymbol);
            }
        }

        public override void DefaultVisit(ISymbol symbol)
        {
            // Run Azure.ClientSdk.Analyzers
            foreach (var rule in _analyzers)
            {
                if (rule.SymbolKinds.Contains(symbol.Kind))
                {
                    rule.Analyze(new Context(symbol, Results));
                }
            }

            // Run Azure.SdkAnalyzers
            foreach (var sdkAnalyzer in _sdkAnalyzers)
            {
                if (sdkAnalyzer.SymbolKinds.Contains(symbol.Kind))
                {
                    sdkAnalyzer.Analyze(symbol, Results);
                }
            }
        }

        private class Context : ISymbolAnalysisContext
        {
            private readonly List<CodeDiagnostic> _results;

            public Context(ISymbol symbol, List<CodeDiagnostic> results)
            {
                Symbol = symbol;
                _results = results;
            }

            public ISymbol Symbol { get; }

            public void ReportDiagnostic(Diagnostic diagnostic, ISymbol symbol)
            {
                _results.Add(new CodeDiagnostic(diagnostic.Id, symbol.GetId(), diagnostic.GetMessage(), diagnostic.Descriptor.HelpLinkUri));
            }
        }
    }
}
