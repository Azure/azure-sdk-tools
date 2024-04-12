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
            "Azure.Compute",
            "Azure.Containers",
            "Azure.Core.Expressions",
            "Azure.Data",
            "Azure.Developer",
            "Azure.DigitalTwins",
            "Azure.Health",
            "Azure.Identity",
            "Azure.IoT",
            "Azure.Maps",
            "Azure.Media",
            "Azure.Messaging",
            "Azure.MixedReality",
            "Azure.Monitor",
            "Azure.ResourceManager",
            "Azure.Search",
            "Azure.Security",
            "Azure.Storage",
            "Azure.Verticals",
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
                // Both the namespace itself or a sub-namespace are valid.
                if (displayString == prefix || displayString.StartsWith(prefix + "."))
                {
                    return;
                }

                // "Azure.Template" is not an approved namespace prefix, but we have a project template by that name
                // to help customers get started. We do not want our template to include a suppression for this
                // descriptor out of the box, so we need to treat it as a special case.
                if (displayString == "Azure.Template" || displayString.StartsWith("Azure.Template."))
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
