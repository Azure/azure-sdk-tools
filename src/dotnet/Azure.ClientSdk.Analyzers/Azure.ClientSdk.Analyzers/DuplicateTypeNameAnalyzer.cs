// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DuplicateTypeNameAnalyzer : SymbolAnalyzerBase
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.AZC0034);
        public override SymbolKind[] SymbolKinds { get; } = { SymbolKind.NamedType };

        // Sorted array of reserved type names loaded from embedded resource
        private static readonly string[] ReservedTypeNames = LoadReservedTypeNames();

        // Names that should only be used as nested types in Azure SDK
        private static readonly HashSet<string> NestedOnlyTypeNames = new HashSet<string>
        {
            "ServiceVersion",
            "Enumerator"
        };

        private static string[] LoadReservedTypeNames()
        {
            var assembly = typeof(DuplicateTypeNameAnalyzer).GetTypeInfo().Assembly;
            var resourceName = "Azure.ClientSdk.Analyzers.reserved-type-names.txt";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    // Fallback to empty array if resource not found
                    return new string[0];
                }
                
                using (var reader = new StreamReader(stream))
                {
                    var content = reader.ReadToEnd();
                    var names = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    Array.Sort(names, StringComparer.Ordinal);
                    return names;
                }
            }
        }



        public override void Analyze(ISymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Only analyze public types in Azure namespaces
            if (namedTypeSymbol.DeclaredAccessibility != Accessibility.Public)
            {
                return;
            }

            // Check if this is in an Azure namespace
            var namespaceName = namedTypeSymbol.ContainingNamespace?.ToDisplayString();
            if (string.IsNullOrEmpty(namespaceName) || !namespaceName.StartsWith("Azure"))
            {
                return;
            }

            var typeName = namedTypeSymbol.Name;

            // Allow exceptions for standard nested types
            if (namedTypeSymbol.ContainingType != null && NestedOnlyTypeNames.Contains(typeName))
            {
                return;
            }

            // Check for types that should only be nested (prioritize this check)
            if (namedTypeSymbol.ContainingType == null && NestedOnlyTypeNames.Contains(typeName))
            {
                foreach (var location in namedTypeSymbol.Locations)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0034, location, typeName), namedTypeSymbol);
                }
                return; // Don't check platform types if already flagged for nested-only
            }

            // Check for conflicts with platform types
            if (Array.BinarySearch(ReservedTypeNames, typeName, StringComparer.Ordinal) >= 0)
            {
                foreach (var location in namedTypeSymbol.Locations)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0034, location, typeName), namedTypeSymbol);
                }
            }
        }
    }
}