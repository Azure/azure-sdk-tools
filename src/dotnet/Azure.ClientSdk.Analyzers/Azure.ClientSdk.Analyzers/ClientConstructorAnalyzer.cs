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

        public override void AnalyzeCore(INamedTypeSymbol type, IAnalysisHost host)
        {
            if (!type.Constructors.Any(c => (c.DeclaredAccessibility == Accessibility.Protected || c.DeclaredAccessibility == Accessibility.Public) && c.Parameters.Length == 0))
            {
                host.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0005, type.Locations.First()), type);
            }

            foreach (var constructor in type.Constructors)
            {
                if (constructor.DeclaredAccessibility == Accessibility.Public)
                {
                    if (IsClientOptionsParameter(constructor.Parameters.LastOrDefault()))
                    {
                        var nonOptionsMethod = FindMethod(
                            type.Constructors, constructor.Parameters.RemoveAt(constructor.Parameters.Length - 1));

                        if (nonOptionsMethod == null || nonOptionsMethod.DeclaredAccessibility != Accessibility.Public)
                        {
                            host.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0006, constructor.Locations.First(), GetOptionsTypeName(type)), constructor);
                        }
                    }
                    else
                    {
                        var optionsMethod = FindMethod(
                            type.Constructors, constructor.Parameters, IsClientOptionsParameter);

                        if (optionsMethod == null || optionsMethod.DeclaredAccessibility != Accessibility.Public)
                        {
                            host.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0007, constructor.Locations.First(), GetOptionsTypeName(type)), constructor);
                        }
                    }
                }
            }
        }
    }
}