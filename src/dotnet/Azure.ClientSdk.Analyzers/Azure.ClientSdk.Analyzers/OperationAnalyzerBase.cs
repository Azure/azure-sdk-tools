// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

namespace Azure.ClientSdk.Analyzers
{
    public abstract class OperationAnalyzerBase : SymbolAnalyzerBase
    {
        protected const string OperationSuffix = "Operation";

        public override SymbolKind[] SymbolKinds { get; } = new[] { SymbolKind.NamedType };

        public override void Analyze(ISymbolAnalysisContext context)
        {
            var typeSymbol = (INamedTypeSymbol)context.Symbol;
            if (typeSymbol.TypeKind != TypeKind.Class ||
                !typeSymbol.Name.EndsWith(OperationSuffix) ||
                typeSymbol.DeclaredAccessibility != Accessibility.Public ||
                typeSymbol.BaseType?.Name != OperationSuffix)
            {
                return;
            }

            AnalyzeCore(context);
        }

        public abstract void AnalyzeCore(ISymbolAnalysisContext context);
    }
}