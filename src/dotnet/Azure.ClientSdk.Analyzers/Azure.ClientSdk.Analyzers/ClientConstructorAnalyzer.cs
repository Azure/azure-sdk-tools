// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
            => symbol != null && IsClientOptionsType(symbol.Type);

        public override void AnalyzeCore(ISymbolAnalysisContext context)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (!type.Constructors.Any(c => (c.DeclaredAccessibility == Accessibility.Protected || c.DeclaredAccessibility == Accessibility.Public) && c.Parameters.Length == 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0005, type.Locations.First()), type);
            }

            foreach (var constructor in type.Constructors)
            {
                if (constructor.DeclaredAccessibility == Accessibility.Public)
                {
                    var lastParameter = constructor.Parameters.LastOrDefault();

                    if (IsClientOptionsParameter(lastParameter))
                    {
                        // Allow optional options parameters
                        if (lastParameter.IsOptional) continue;

                        // When there are static properties in client, there would be static constructor implicitly added
                        var nonOptionsMethod = FindMethod(
                            type.Constructors, constructor.TypeParameters, constructor.Parameters.RemoveAt(constructor.Parameters.Length - 1), true);

                        if (nonOptionsMethod == null || nonOptionsMethod.DeclaredAccessibility != Accessibility.Public)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0006, constructor.Locations.First()), constructor);
                        }
                    }
                    else
                    {
                        var optionsMethod = FindMethod(
                            type.Constructors, constructor.TypeParameters, constructor.Parameters, IsClientOptionsParameter);

                        if (optionsMethod == null || optionsMethod.DeclaredAccessibility != Accessibility.Public)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0007, constructor.Locations.First()), constructor);
                        }
                    }
                }
            }
        }
    }
}
