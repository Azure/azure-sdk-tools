// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers.ModelName
{
    /// <summary>
    /// Analyzer to check model names ending with "Operation". Avoid using Operation as model suffix unless the model derives from Operation
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OperationSuffixAnalyzer : SuffixAnalyzerBase
    {
        public const string DiagnosticId = nameof(AZC0033);

        private static readonly string messageFormat = "Model name '{0}' ends with '{1}'. Suggest to rename it to '{2}' or '{3}', if an appropriate name could not be found.";

        private static readonly DiagnosticDescriptor AZC0033 = new DiagnosticDescriptor(DiagnosticId, Title,
            messageFormat, DiagnosticCategory.Naming, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: Description);

        private static readonly Regex operationSuffixRegex = new Regex(".+(?<Suffix>(Operation))$");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(AZC0033); } }

        // Unless the model derivew from Operation
        protected override bool ShouldSkip(INamedTypeSymbol symbol, SymbolAnalysisContext context) => IsTypeOf(symbol, "Azure", "Operation");

        protected override Regex SuffixRegex => operationSuffixRegex;
        protected override Diagnostic GetDiagnostic(INamedTypeSymbol typeSymbol, string suffix, SymbolAnalysisContext context)
        {
            var name = typeSymbol.Name;
            var nameWithoutSuffix = name.Substring(0, name.Length - suffix.Length);
            return Diagnostic.Create(AZC0033, context.Symbol.Locations[0],
                name, suffix, $"{nameWithoutSuffix}Data", $"{nameWithoutSuffix}Info");
        }
    }
}
