// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers.ModelName
{
    /// <summary>
    /// Analyzer to check general model name suffix issues.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class GeneralSuffixAnalyzer : SuffixAnalyzerBase
    {
        private static readonly ImmutableHashSet<string> reservedNames = ImmutableHashSet.Create("ErrorResponse");

        // Avoid to use suffixes "Request(s)", "Parameter(s)", "Option(s)", "Response(s)", "Collection"
        private static readonly string[] generalSuffixes = new string[] { "Request", "Requests", "Response", "Responses", "Parameter", "Parameters", "Option", "Options", "Collection"};

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Descriptors.AZC0030); } }

        protected override bool ShouldSkip(INamedTypeSymbol symbol, SymbolAnalysisContext context) => reservedNames.Contains(symbol.Name);

        protected override string[] SuffixesToCatch => generalSuffixes;
        protected override Diagnostic GetDiagnostic(INamedTypeSymbol typeSymbol, string suffix, SymbolAnalysisContext context)
        {
            var name = typeSymbol.Name;
            var suggestedName = GetSuggestedName(name, suffix);
            return Diagnostic.Create(Descriptors.AZC0030, context.Symbol.Locations[0],
                new Dictionary<string, string> { { "SuggestedName", suggestedName } }.ToImmutableDictionary(), name, suffix, suggestedName);
        }

        private string GetSuggestedName(string originalName, string suffix)
        {
            var nameWithoutSuffix = originalName.Substring(0, originalName.Length - suffix.Length);
            return suffix switch
            {
                "Request" or "Requests" => $"'{nameWithoutSuffix}Content'",
                "Parameter" or "Parameters" => $"'{nameWithoutSuffix}Content' or '{nameWithoutSuffix}Patch'",
                "Option" or "Options" => $"'{nameWithoutSuffix}Config'",
                "Response" => $"'{nameWithoutSuffix}Result'",
                "Responses" => $"'{nameWithoutSuffix}Results'",
                "Collection" => $"'{nameWithoutSuffix}Group' or '{nameWithoutSuffix}List'",
                _ => nameWithoutSuffix,
            };
        }
    }
}
