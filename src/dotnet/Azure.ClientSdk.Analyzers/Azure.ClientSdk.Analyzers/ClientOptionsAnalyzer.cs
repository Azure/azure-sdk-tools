// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ClientOptionsAnalyzer : DiagnosticAnalyzer
    {
        protected const string ClientOptionsSuffix = "ClientOptions";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Descriptors.AZC0008
        });

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(
                analysisContext =>
                {
                    analysisContext.RegisterSymbolAction(symbolAnalysisContext =>
                    {
                        var typeSymbol = (INamedTypeSymbol)symbolAnalysisContext.Symbol;
                        if (typeSymbol.TypeKind != TypeKind.Class || !typeSymbol.Name.EndsWith(ClientOptionsSuffix) || typeSymbol.DeclaredAccessibility != Accessibility.Public)
                        {
                            return;
                        }

                        AnalyzeClientOptionsType(symbolAnalysisContext);
                    }, SymbolKind.NamedType);
                });
        }

        private void AnalyzeClientOptionsType(SymbolAnalysisContext context)
        {
            var typeSymbol = (INamedTypeSymbol)context.Symbol;

            var serviceVersion = typeSymbol.GetTypeMembers("ServiceVersion");
            var serviceVersionEnum = serviceVersion.SingleOrDefault(member => member.TypeKind == TypeKind.Enum);
            if (serviceVersionEnum == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0008, typeSymbol.Locations.First()));
            }
        }
    }
}