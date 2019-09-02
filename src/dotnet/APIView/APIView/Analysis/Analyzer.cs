using ApiView;
using Azure.ClientSdk.Analyzers;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace APIView.Analysis
{
    class Analyzer : IAnalysisHost
    {
        List<AnalysisResult> _results = new List<AnalysisResult>();
        List<IHostedAnalyzer> _analyzers = new List<IHostedAnalyzer>();

        public Analyzer(IAssemblySymbol assembly)
        {
            if (assembly.Name.StartsWith("Azure")) {
                _analyzers.Add(new ClientMethodsAnalyzer());
                _analyzers.Add(new ClientConstructorAnalyzer());
            }
        }

        public void Analyze(INamedTypeSymbol type)
        {
            foreach (var rule in _analyzers)
            {
                rule.Analyze(type, this);
            }
        }

        public AnalysisResult[] CreateResults()
        {
            return _results.ToArray();
        }

        public void ReportDiagnostic(Diagnostic diagnostic, ISymbol symbol)
        {
            var result = new AnalysisResult(symbol.GetId(), diagnostic.Descriptor.Title.ToString(), diagnostic.Descriptor.HelpLinkUri);
            _results.Add(result);
        }
    }
}
