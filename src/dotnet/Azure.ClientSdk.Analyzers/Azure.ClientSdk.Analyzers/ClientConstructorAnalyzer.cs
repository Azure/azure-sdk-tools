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
            Descriptors.AZC0007,
            Descriptors.AZC0021
        });

        private bool IsClientOptionsParameter(IParameterSymbol symbol) 
            => symbol != null && IsClientOptionsType(symbol.Type);

        private bool IsClientSettingsParameter(IParameterSymbol symbol) 
            => symbol != null && IsClientSettingsType(symbol.Type);

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

                    // Check if any parameter is ClientSettings - it should only be used alone
                    if (constructor.Parameters.Any(IsClientSettingsParameter) && constructor.Parameters.Length > 1)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0021, constructor.Locations.First()), constructor);
                        continue;
                    }

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
                    else if (IsClientSettingsParameter(lastParameter))
                    {
                        // Allow constructors ending with ClientSettings parameter without requiring a ClientOptions overload
                        // This is the new pattern for System.ClientModel.ClientSettings
                        // Note: AZC0021 already checked for multiple parameters above
                        continue;
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
