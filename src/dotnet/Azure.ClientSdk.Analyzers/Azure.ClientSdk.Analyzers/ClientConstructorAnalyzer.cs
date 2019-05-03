// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ClientConstructorAnalyzer : ClientAnalyzerBase
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Descriptors.AZC0005,
            Descriptors.AZC0006,
            Descriptors.AZC0007
        });

        protected override void AnalyzeClientType(SymbolAnalysisContext context)
        {
            var typeSymbol = (INamedTypeSymbol)context.Symbol;
            if (!typeSymbol.Constructors.Any(c => (c.DeclaredAccessibility == Accessibility.Protected || c.DeclaredAccessibility == Accessibility.Public) && c.Parameters.Length == 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0005, typeSymbol.Locations.First()));
            }

            foreach (var constructor in typeSymbol.Constructors)
            {
                if (constructor.DeclaredAccessibility == Accessibility.Public)
                {
                    if (IsClientOptionsParameter(constructor.Parameters.LastOrDefault()))
                    {
                        var nonOptionsMethod = FindMethod(
                            typeSymbol.Constructors, constructor.Parameters.RemoveAt(constructor.Parameters.Length - 1));

                        if (nonOptionsMethod == null || nonOptionsMethod.DeclaredAccessibility != Accessibility.Public)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0006, constructor.Locations.First(), GetOptionsTypeName(typeSymbol)));
                        }
                    }
                    else
                    {
                        var optionsMethod = FindMethod(
                            typeSymbol.Constructors, constructor.Parameters, IsClientOptionsParameter);

                        if (optionsMethod == null || optionsMethod.DeclaredAccessibility != Accessibility.Public)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0007, constructor.Locations.First(), GetOptionsTypeName(typeSymbol)));
                        }
                    }
                }
            }
        }

        private bool IsClientOptionsParameter(IParameterSymbol symbol)
        {
            if (symbol == null)
            {
                return false;
            }
            var clientOptionsType = GetOptionsTypeName(symbol.ContainingType);
            return symbol.Type.Name == clientOptionsType;
        }

        private static string GetOptionsTypeName(INamedTypeSymbol symbol)
        {
            return symbol.Name + "Options";
        }
    }
}