// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers.ModelName
{
    /// <summary>
    /// Analyzer that checks model names ending with "Options". This analyzer uses its own diagnostic ID AZC0036.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OptionsSuffixAnalyzer : SuffixAnalyzerBase
    {
        private const string AzureResourceManagerNamespaceName = "Azure.ResourceManager";
        private const string OptionsSuffix = "Options";
        private const string IUtf8JsonSerializable = "IUtf8JsonSerializable";
        private const string JsonElement = "JsonElement";
        private static readonly string[] suffixes = new string[] { OptionsSuffix };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.AZC0036);

        protected override bool ShouldSkip(INamedTypeSymbol symbol, SymbolAnalysisContext context)
        {
            // skip property bag classes which have `Options` suffix and either:
                // 1. not in Azure.ResourceManager namespace
                // 2. don't have serialization
            if (symbol.Name.EndsWith(OptionsSuffix) && (!IsInManagementNamespace(symbol) || !SupportSerialization(symbol)))
                return true;

            return false;
        }

        private bool SupportSerialization(INamedTypeSymbol symbol)
        {
            // if it has serialization method: `IUtf8JsonSerializable.Write`, e.g. ": IUtf8JsonSerializable"
            if (symbol.Interfaces.Any(i => i.Name is IUtf8JsonSerializable))
                return true;

            // if it has deserialization method: static <T> Deserialize<T>(JsonElement element)
            if (symbol.GetMembers($"Deserialize{symbol.Name}").Any(m => m is IMethodSymbol methodSymbol &&
                methodSymbol is { IsStatic: true, ReturnType: INamedTypeSymbol symbol, Parameters.Length: 1 } &&
                methodSymbol.Parameters[0].Type.Name is JsonElement))
                return true;

            return false;
        }

        private bool IsInManagementNamespace(INamedTypeSymbol symbol)
        {
            if (symbol.ContainingNamespace == null || symbol.ContainingNamespace.IsGlobalNamespace)
                return false;

            var fullNamespace = symbol.ContainingNamespace.GetFullNamespaceName();
            return $"{fullNamespace}".Equals(AzureResourceManagerNamespaceName)
                || $"{fullNamespace}".StartsWith($"{AzureResourceManagerNamespaceName}.");
        }

        protected override string[] SuffixesToCatch => suffixes;
        protected override Diagnostic GetDiagnostic(INamedTypeSymbol typeSymbol, string suffix, SymbolAnalysisContext context)
        {
            var suggestedName = NamingSuggestionHelper.GetNamespacedSuggestion(typeSymbol.Name, typeSymbol, "Settings", "Config");
            var additionalMessage = $"The `{suffix}` suffix is reserved for input models described by " +
                $"https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-parameters. Please rename `{typeSymbol.Name}` " +
                $"to {suggestedName} or another suitable name according to our guidelines at " +
                $"https://azure.github.io/azure-sdk/general_design.html#model-types for output or roundtrip models.";
            return Diagnostic.Create(Descriptors.AZC0036, context.Symbol.Locations[0], typeSymbol.Name, suffix, additionalMessage);
        }
    }
}
