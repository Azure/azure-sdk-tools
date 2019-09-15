// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

namespace Azure.ClientSdk.Analyzers
{
    public interface ISymbolAnalysisContext
    {
        ISymbol Symbol { get; }
        void ReportDiagnostic(Diagnostic diagnostic, ISymbol symbol);
    }
}