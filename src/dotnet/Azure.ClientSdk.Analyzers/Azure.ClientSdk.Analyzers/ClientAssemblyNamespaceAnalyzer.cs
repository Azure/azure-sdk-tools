// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ClientAssemblyNamespaceAnalyzer : SymbolAnalyzerBase
    {
        internal static readonly string[] AllowedNamespacePrefix = new[]
        {
            "Azure.AI",
            "Azure.Analytics",
            "Azure.Communication",
            "Azure.Data",
            "Azure.DigitalTwins",
            "Azure.IoT",
            "Azure.Learn",
            "Azure.Media",
            "Azure.Management",
            "Azure.Messaging",
            "Azure.ResourceManager",   
            "Azure.Search",
            "Azure.Security",
            "Azure.Storage",
            "Azure.Template",
            "Azure.Identity",
            "Microsoft.Extensions.Azure"
        };

        public ClientAssemblyNamespaceAnalyzer()
        {
            SupportedDiagnostics = ImmutableArray.Create(new[]
            {
                Descriptors.AZC0001
            });
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

        public override SymbolKind[] SymbolKinds { get; } = new[] { SymbolKind.Namespace };

        public override void Analyze(ISymbolAnalysisContext context)
        {
            var namespaceSymbol = (INamespaceSymbol)context.Symbol;
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
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0001, namespaceSymbolLocation, displayString), namespaceSymbol);
            }
        }
    }
}
