// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ApiView;
using Azure.ClientSdk.Analyzers;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace APIView.Analysis
{
    internal class Analyzer : SymbolVisitor
    {
        public List<CodeDiagnostic> Results { get; } = new List<CodeDiagnostic>();

        private readonly List<SymbolAnalyzerBase> _analyzers = new List<SymbolAnalyzerBase>();

        public Analyzer()
        {
            _analyzers.Add(new ClientMethodsAnalyzer());
            _analyzers.Add(new ClientConstructorAnalyzer());
            _analyzers.Add(new ClientOptionsAnalyzer());
            _analyzers.Add(new ClientAssemblyNamespaceAnalyzer());
            _analyzers.Add(new BannedAssembliesAnalyzer());
            _analyzers.Add(new TypeNameAnalyzer());
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
            foreach (var rule in _analyzers)
            {
                if (rule.SymbolKinds.Contains(symbol.Kind))
                {
                    rule.Analyze(new Context(symbol, Results));
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
