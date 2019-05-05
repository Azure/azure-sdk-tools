// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ClientAssemblyNamespaceAnalyzer : DiagnosticAnalyzer
    {
        internal static readonly string[] AllowedNamespacePrefix = new[]
        {
            "Azure.ApplicationModel",
            "Azure.Analytics",
            "Azure.Data",
            "Azure.Iot",
            "Azure.Media",
            "Azure.Messaging",
            "Azure.ML",
            "Azure.Security",
            "Azure.Storage"
        };

        public ClientAssemblyNamespaceAnalyzer()
        {
            SupportedDiagnostics = ImmutableArray.Create(new[]
            {
                Descriptors.AZC0001
            });
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(
                analysisContext => {
                    analysisContext.RegisterSymbolAction(symbolAnalysisContext => AnalyzeNamespace(symbolAnalysisContext), SymbolKind.Namespace);
                });
        }

        private void AnalyzeNamespace(SymbolAnalysisContext symbolAnalysisContext)
        {
            var namespaceSymbol = (INamespaceSymbol)symbolAnalysisContext.Symbol;
            bool hasPublicTypes = false;
            foreach (var member in namespaceSymbol.GetMembers())
            {
                if (member.IsType && member.DeclaredAccessibility == Accessibility.Public)
                {
                    hasPublicTypes = true;
                    break;
                }
            }

            if (!hasPublicTypes)
            {
                return;
            }

            var displayString = namespaceSymbol.ToDisplayString();
            foreach (var prefix in AllowedNamespacePrefix)
            {
                if (displayString.StartsWith(prefix))
                {
                    return;
                }
            }

            foreach (var namespaceSymbolLocation in namespaceSymbol.Locations)
            {
                symbolAnalysisContext.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0001, namespaceSymbolLocation, displayString));
            }
        }
    }
}
