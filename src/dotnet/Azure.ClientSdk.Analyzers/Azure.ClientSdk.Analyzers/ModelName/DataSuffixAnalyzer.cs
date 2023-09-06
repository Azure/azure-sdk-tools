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
        public const string DiagnosticId = nameof(AZC0032);

        private static readonly DiagnosticDescriptor AZC0032 = new DiagnosticDescriptor(DiagnosticId, Title,
            GeneralRenamingMessageFormat, DiagnosticCategory.Naming, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: Description);

        private static readonly Regex DataSuffixRegex = new Regex(".+(?<Suffix>(Data))$");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(AZC0032); } }

        // unless the model derives from ResourceData/TrackedResourceData
        protected override bool ShouldSkip(INamedTypeSymbol symbol, SymbolAnalysisContext context) => IsTypeOf(symbol, "Azure.ResourceManager.Models", "ResourceData") ||
            IsTypeOf(symbol, "Azure.ResourceManager.Models", "TrackedResourceData");

        protected override Regex SuffixRegex => DataSuffixRegex;

        protected override Diagnostic GetDiagnostic(INamedTypeSymbol typeSymbol, string suffix, SymbolAnalysisContext context)
        {
            var name = typeSymbol.Name;
            return Diagnostic.Create(AZC0032, context.Symbol.Locations[0],
                new Dictionary<string, string> { { "SuggestedName", name.Substring(0, name.Length - suffix.Length) } }.ToImmutableDictionary(), name, suffix);
        }
    }
}
