// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FileTypeNameAnalyzer : DiagnosticAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(
                analysisContext => {
                    analysisContext.RegisterSymbolAction(
                        symbolAnalysisContext => {
                            var symbol = symbolAnalysisContext.Symbol;

                            if (symbol.DeclaredAccessibility != Accessibility.Public)
                            {
                                return;
                            }

                            // Skip nested types
                            if (symbol.ContainingType != null)
                            {
                                return;
                            }

                            foreach (var symbolLocation in symbol.Locations)
                            {
                                var fileName = Path.GetFileName(symbolLocation.SourceTree.FilePath);
                                if (fileName.IndexOf(symbol.Name, StringComparison.OrdinalIgnoreCase) == -1)
                                {
                                    symbolAnalysisContext.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC1003, symbolLocation, symbol.Name));
                                }
                            }
                        }, SymbolKind.NamedType);
                });
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(new[]
        {
            Descriptors.AZC1003
        });
    }
}