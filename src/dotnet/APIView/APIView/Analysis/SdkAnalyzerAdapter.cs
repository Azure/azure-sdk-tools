// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ApiView;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace APIView.Analysis
{
    /// <summary>
    /// Adapter that allows Azure.SdkAnalyzers (which use SymbolAnalysisContext) to be used
    /// in APIView's analysis pipeline alongside Azure.ClientSdk.Analyzers (which use ISymbolAnalysisContext).
    /// </summary>
    internal class SdkAnalyzerAdapter
    {
        private readonly Azure.SdkAnalyzers.SymbolAnalyzerBase _analyzer;

        public SdkAnalyzerAdapter(Azure.SdkAnalyzers.SymbolAnalyzerBase analyzer)
        {
            _analyzer = analyzer;
        }

        public SymbolKind[] SymbolKinds => _analyzer.SymbolKinds;

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => _analyzer.SupportedDiagnostics;

        /// <summary>
        /// Analyzes a symbol and reports diagnostics to the results list.
        /// </summary>
        public void Analyze(ISymbol symbol, List<CodeDiagnostic> results)
        {
            // Create a SymbolAnalysisContext that captures diagnostics to our results list
            Action<Diagnostic> reportDiagnostic = d =>
            {
                results.Add(new CodeDiagnostic(
                    d.Id,
                    symbol.GetId(),
                    d.GetMessage(),
                    d.Descriptor.HelpLinkUri));
            };

            var context = new SymbolAnalysisContext(
                symbol,
                compilation: null,
                options: null,
                reportDiagnostic: reportDiagnostic,
                isSupportedDiagnostic: _ => true,
                cancellationToken: CancellationToken.None);

            _analyzer.Analyze(context);
        }
    }
}
