// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
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

        // Avoid to use suffixes "Request(s)", "Parameter(s)", "Option", "Response(s)", "Collection"
        private static readonly string[] generalSuffixes = new string[] { "Request", "Requests", "Response", "Responses", "Parameter", "Parameters", "Option", "Collection" };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Descriptors.AZC0030); } }

        protected override bool ShouldSkip(INamedTypeSymbol symbol, SymbolAnalysisContext context)
        {
            if (reservedNames.Contains(symbol.Name))
                return true;

            return false;
        }

        protected override string[] SuffixesToCatch => generalSuffixes;
        protected override Diagnostic GetDiagnostic(INamedTypeSymbol typeSymbol, string suffix, SymbolAnalysisContext context)
        {
            var name = typeSymbol.Name;
            var suggestedName = GetSuggestedName(name, suffix, typeSymbol);
            var additionalMessage = $"We suggest renaming it to {suggestedName} or another name with this suffix.";
            return Diagnostic.Create(Descriptors.AZC0030, context.Symbol.Locations[0],
                new Dictionary<string, string> { { "SuggestedName", suggestedName } }.ToImmutableDictionary(), name, suffix, additionalMessage);
        }

        private string GetSuggestedName(string originalName, string suffix, INamedTypeSymbol typeSymbol)
        {
            var nameWithoutSuffix = originalName.Substring(0, originalName.Length - suffix.Length);
            return suffix switch
            {
                "Request" or "Requests" => NamingSuggestionHelper.GetNamespacedSuggestion(originalName, typeSymbol, "Content"),
                "Parameter" or "Parameters" => NamingSuggestionHelper.GetNamespacedSuggestion(originalName, typeSymbol, "Content", "Patch"),
                "Option" => NamingSuggestionHelper.GetNamespacedSuggestion(originalName, typeSymbol, "Config"),
                "Response" => NamingSuggestionHelper.GetNamespacedSuggestion(originalName, typeSymbol, "Result"),
                "Responses" => NamingSuggestionHelper.GetNamespacedSuggestion(originalName, typeSymbol, "Results"),
                "Collection" => NamingSuggestionHelper.GetNamespacedSuggestion(originalName, typeSymbol, "Group", "List"),
                _ => nameWithoutSuffix,
            };
        }
    }
}
