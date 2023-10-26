// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Azure.ClientSdk.Analyzers.ModelName
{
    // 
    /// <summary>
    /// Analyzer to check the model names ending with "Data". Avoid using "Data" as model suffix unless the model derives from ResourceData/TrackedResourceData.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DataSuffixAnalyzer : SuffixAnalyzerBase
    {
        private static readonly string[] dataSuffix = new string[] { "Data" };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Descriptors.AZC0032); } }

        // unless the model derives from ResourceData/TrackedResourceData
        protected override bool ShouldSkip(INamedTypeSymbol symbol, SymbolAnalysisContext context) => IsTypeOf(symbol, "Azure.ResourceManager.Models", "ResourceData") ||
            IsTypeOf(symbol, "Azure.ResourceManager.Models", "TrackedResourceData");

        protected override string[] SuffixesToCatch => dataSuffix;

        protected override Diagnostic GetDiagnostic(INamedTypeSymbol typeSymbol, string suffix, SymbolAnalysisContext context)
        {
            var name = typeSymbol.Name;
            return Diagnostic.Create(Descriptors.AZC0032, context.Symbol.Locations[0],
                new Dictionary<string, string> { { "SuggestedName", name.Substring(0, name.Length - suffix.Length) } }.ToImmutableDictionary(), name, suffix);
        }
    }
}
