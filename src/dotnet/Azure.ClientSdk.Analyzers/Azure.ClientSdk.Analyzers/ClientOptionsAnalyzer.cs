// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ClientOptionsAnalyzer : SymbolAnalyzerBase
    {
        protected const string ServiceVersionName = "ServiceVersion";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Descriptors.AZC0008,
            Descriptors.AZC0009,
            Descriptors.AZC0010,
            Descriptors.AZC0016,
        });

        public override SymbolKind[] SymbolKinds => new[] { SymbolKind.NamedType };

        public override void Analyze(ISymbolAnalysisContext symbolAnalysisContext)
        {
            var typeSymbol = (INamedTypeSymbol)symbolAnalysisContext.Symbol;
            if (IsClientOptionsType(typeSymbol))
            {
                AnalyzeClientOptionsType(symbolAnalysisContext);
            }
        }

        private void AnalyzeClientOptionsType(ISymbolAnalysisContext context)
        {
            var typeSymbol = (INamedTypeSymbol)context.Symbol;

            var serviceVersion = typeSymbol.GetTypeMembers(ServiceVersionName);
            var serviceVersionEnum = serviceVersion.SingleOrDefault(member => member.TypeKind == TypeKind.Enum);
            if (serviceVersionEnum == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0008, typeSymbol.Locations.First()), typeSymbol);
                return;
            }

            foreach (var serviceVersionMember in serviceVersionEnum.GetMembers().Where(member => member.Kind == SymbolKind.Field))
            {
                var parts = serviceVersionMember.Name.Split('_');
                foreach (var part in parts)
                {
                    if (part.Length == 0 || !(char.IsDigit(part[0]) || char.IsUpper(part[0])))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0016, serviceVersionMember.Locations.First()), serviceVersionMember);
                    }
                }
            }

            foreach (var constructor in typeSymbol.Constructors)
            {
                if (constructor.DeclaredAccessibility == Accessibility.Public)
                {
                    if (constructor.Parameters == null || constructor.Parameters.Length == 0)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0009, constructor.Locations.First()), typeSymbol);
                        continue;
                    }

                    var firstParam = constructor.Parameters.FirstOrDefault();
                    if (!IsServiceVersionParameter(firstParam))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0009, firstParam.Locations.First()), typeSymbol);
                        continue;
                    }

                    var maxVersion = serviceVersionEnum.GetMembers().Where(m => m.Kind == SymbolKind.Field).Max(m => ((IFieldSymbol)m).ConstantValue);
                    if (!firstParam.HasExplicitDefaultValue || (int)firstParam.ExplicitDefaultValue != (int)maxVersion)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0010, firstParam.Locations.First()), typeSymbol);
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