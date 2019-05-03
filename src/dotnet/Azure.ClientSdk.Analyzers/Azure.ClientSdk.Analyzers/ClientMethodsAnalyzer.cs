// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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

        protected override void AnalyzeClientType(SymbolAnalysisContext context)
        {
            var typeSymbol = (INamedTypeSymbol)context.Symbol;
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is IMethodSymbol methodSymbol && methodSymbol.Name.EndsWith(AsyncSuffix) && methodSymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    CheckClientMethod(context, methodSymbol);

                    var syncMemberName = member.Name.Substring(0, member.Name.Length - AsyncSuffix.Length);

                    var syncMember = FindMethod(typeSymbol.GetMembers(syncMemberName).OfType<IMethodSymbol>(), methodSymbol.Parameters);

                    if (syncMember == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0004, member.Locations.First()));
                    }
                    else
                    {
                        CheckClientMethod(context, syncMember);
                    }
                }
            }
        }

        private static void CheckClientMethod(SymbolAnalysisContext context, IMethodSymbol member)
        {
            if (!member.IsVirtual)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0003, member.Locations.First()));
            }

            var lastArgument = member.Parameters.LastOrDefault();
            if (lastArgument == null || lastArgument.Name != "cancellationToken" || lastArgument.Type.Name != "CancellationToken" || !lastArgument.HasExplicitDefaultValue)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0002, member.Locations.First()));
            }
        }
    }
}