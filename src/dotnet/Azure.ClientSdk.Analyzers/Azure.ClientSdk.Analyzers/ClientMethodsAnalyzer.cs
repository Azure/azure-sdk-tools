// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{   
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ClientMethodsAnalyzer : ClientAnalyzerBase
    {
        private const string AsyncSuffix = "Async";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Descriptors.AZC0002,
            Descriptors.AZC0003,
            Descriptors.AZC0004
        });

        private static void CheckClientMethod(IAnalysisHost host, IMethodSymbol member)
        {
            if (!member.IsVirtual)
            {
                var diagnostic = Diagnostic.Create(Descriptors.AZC0003, member.Locations.First());
                host.ReportDiagnostic(diagnostic, member);
            }

            var lastArgument = member.Parameters.LastOrDefault();
            if (lastArgument == null || lastArgument.Name != "cancellationToken" || lastArgument.Type.Name != "CancellationToken" || !lastArgument.HasExplicitDefaultValue)
            {
                var diagnostic = Diagnostic.Create(Descriptors.AZC0002, member.Locations.First());
                host.ReportDiagnostic(diagnostic, member);
            }
        }

        public override void AnalyzeCore(INamedTypeSymbol type, IAnalysisHost host)
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IMethodSymbol methodSymbol && methodSymbol.Name.EndsWith(AsyncSuffix) && methodSymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    CheckClientMethod(host, methodSymbol);

                    var syncMemberName = member.Name.Substring(0, member.Name.Length - AsyncSuffix.Length);

                    var syncMember = FindMethod(type.GetMembers(syncMemberName).OfType<IMethodSymbol>(), methodSymbol.Parameters);

                    if (syncMember == null)
                    {
                        host.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0004, member.Locations.First()), member);
                    }
                    else
                    {
                        CheckClientMethod(host, syncMember);
                    }
                }
            }
        }
    }
}