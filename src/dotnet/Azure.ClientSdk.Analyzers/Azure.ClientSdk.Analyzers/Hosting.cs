using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    public interface IHostedAnalyzer
    {
        void Analyze(INamedTypeSymbol type, IAnalysisHost host);
    }

    public interface IAnalysisHost
    {
        void ReportDiagnostic(Diagnostic diagnostic, ISymbol target);
    }

    class AnalysisHost : IAnalysisHost
    {
        SymbolAnalysisContext _roslynHost;

        public AnalysisHost(SymbolAnalysisContext roslynHost)
        {
            _roslynHost = roslynHost;
        }

        public void ReportDiagnostic(Diagnostic diagnostic, ISymbol target)
        {
            _roslynHost.ReportDiagnostic(diagnostic);
        }
    }
}
