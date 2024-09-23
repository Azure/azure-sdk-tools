// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers.ModelName
{
    /// <summary>
    /// Analyzer to check model names ending with "Definition". Avoid using "Definition" as model suffix unless it's the name of a Resource or
    /// after removing the suffix it's another type.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DefinitionSuffixAnalyzer : SuffixAnalyzerBase
    {
        private static readonly string[] definitionSuffix = new string[] { "Definition" };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Descriptors.AZC0031); } }

        // unless the type is a Resource or after removing the suffix it's another type
        protected override bool ShouldSkip(INamedTypeSymbol symbol, SymbolAnalysisContext context)
        {
            if (IsTypeOf(symbol, "Azure.ResourceManager", "ArmResource"))
                return true;

            var name = symbol.Name;
            var suggestedName = name.AsSpan().Slice(0, name.Length - "Definition".Length);

            var strBuilder = symbol.ContainingNamespace.GetFullNamespaceName();
            strBuilder.Append('.').Append(suggestedName.ToArray());

            return context.Compilation.GetTypeByMetadataName(strBuilder.ToString()) is not null;
        }

        protected override string[] SuffixesToCatch => definitionSuffix;

        protected override Diagnostic GetDiagnostic(INamedTypeSymbol typeSymbol, string suffix, SymbolAnalysisContext context)
        {
            var name = typeSymbol.Name;
            return Diagnostic.Create(Descriptors.AZC0031, context.Symbol.Locations[0],
                new Dictionary<string, string> { { "SuggestedName", name.Substring(0, name.Length - suffix.Length) } }.ToImmutableDictionary(), name, suffix);
        }
    }
}
