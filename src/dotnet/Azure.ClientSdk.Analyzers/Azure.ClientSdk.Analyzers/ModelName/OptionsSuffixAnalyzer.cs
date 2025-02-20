// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers.ModelName
{
    /// <summary>
    /// Analyzer to check model names ending with "Options". This analyzer shares the diagnostic ID with <see cref="GeneralSuffixAnalyzer"/>.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OptionsSuffixAnalyzer : SuffixAnalyzerBase
    {
        private const string AzureNamespaceName = "Azure";
        private const string ResourceManagerNamespaceName = "ResourceManager";
        private const string OptionsSuffix = "Options";
        private static readonly string[] suffixes = new string[] { OptionsSuffix };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Descriptors.AZC0030); } }

        protected override bool ShouldSkip(INamedTypeSymbol symbol, SymbolAnalysisContext context)
        {
            // skip property bag classes which have `Options` suffix and:
                // 1. not in Azure.ResourceManager namespace
                // 2. not does not have serialization
            if (symbol.Name.EndsWith(OptionsSuffix) && (!IsInManagementNamespace(symbol) || !SupportSerialization(symbol)))
                return true;

            return false;
        }

        private bool SupportSerialization(INamedTypeSymbol symbol)
        {
            // if it has serialization method: `IUtf8JsonSerializable.Write`, e.g. ": IUtf8JsonSerializable"
            if (symbol.Interfaces.Any(i => i.Name is "IUtf8JsonSerializable"))
                return true;

            // if it has deserialization method: static <T> Deserialize<T>(JsonElement element)
            if (symbol.GetMembers($"Deserialize{symbol.Name}").Any(m => m is IMethodSymbol methodSymbol &&
                methodSymbol is { IsStatic: true, ReturnType: INamedTypeSymbol symbol, Parameters.Length: 1 } &&
                methodSymbol.Parameters[0].Type.Name is "JsonElement"))
                return true;

            return false;
        }

        private bool IsInManagementNamespace(INamedTypeSymbol symbol)
        {
            var currentNamespace = symbol.ContainingNamespace;
            if (currentNamespace == null || currentNamespace.IsGlobalNamespace)
                return false;

            var namespaces = new Stack<string>();

            while (currentNamespace != null && currentNamespace.IsGlobalNamespace is false)
            {
                namespaces.Push(currentNamespace.Name);
                currentNamespace = currentNamespace.ContainingNamespace;
            }

            if (namespaces.Count < 2)
                return false;

            // check if the top two namespace names are Azure + ResourceManager
            var topLevel = namespaces.Pop();
            var secondLevel = namespaces.Pop();
            return topLevel == AzureNamespaceName && secondLevel == ResourceManagerNamespaceName;
        }

        protected override string[] SuffixesToCatch => suffixes;
        protected override Diagnostic GetDiagnostic(INamedTypeSymbol typeSymbol, string suffix, SymbolAnalysisContext context)
        {
            var additionalMessage = $"The `{suffix}` suffix is reserved for input models described by " +
                $"https://azure.github.io/azure-sdk/dotnet_introduction.html#dotnet-parameters. Please rename `{typeSymbol.Name}` " +
                $"according to our guidelines at https://azure.github.io/azure-sdk/general_design.html#model-types for output or roundtrip models.";
            return Diagnostic.Create(Descriptors.AZC0030, context.Symbol.Locations[0], typeSymbol.Name, suffix, additionalMessage);
        }
    }
}
