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
        protected const string ServiceVersionName = "ServiceVersion";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Descriptors.AZC0008,
            Descriptors.AZC0009,
            Descriptors.AZC0010
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

            var serviceVersion = typeSymbol.GetTypeMembers(ServiceVersionName);
            var serviceVersionEnum = serviceVersion.SingleOrDefault(member => member.TypeKind == TypeKind.Enum);
            if (serviceVersionEnum == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0008, typeSymbol.Locations.First()));
                return;
            }

            foreach (var constructor in typeSymbol.Constructors)
            {
                if (constructor.DeclaredAccessibility == Accessibility.Public)
                {
                    if (constructor.Parameters == null || constructor.Parameters.Length == 0)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0009, constructor.Locations.First()));
                        continue;
                    }

                    var firstParam = constructor.Parameters.FirstOrDefault();
                    if (!IsServiceVersionParameter(firstParam))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0009, firstParam.Locations.First()));
                        continue;
                    }

                    if (!firstParam.HasExplicitDefaultValue)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0010, firstParam.Locations.First()));
                    }
                }
            }
        }

        private bool IsServiceVersionParameter(IParameterSymbol symbol)
        {
            if (symbol == null)
            {
                return false;
            }

            return (symbol.Type.Name == ServiceVersionName && symbol.Type.TypeKind == TypeKind.Enum);
        }
    }
}