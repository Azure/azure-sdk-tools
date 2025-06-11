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
        
        // Parallel array of qualified type names corresponding to the reserved type names
        private static readonly string[] QualifiedTypeNames = LoadQualifiedTypeNames();

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
                    var names = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            names.Add(line);
                        }
                    }
                    var nameArray = names.ToArray();
                    
                    VerifyNamesSorted(nameArray);
                    
                    return nameArray;
                }
            }
        }

        private static string[] LoadQualifiedTypeNames()
        {
            var assembly = typeof(DuplicateTypeNameAnalyzer).GetTypeInfo().Assembly;
            var resourceName = "Azure.ClientSdk.Analyzers.reserved-type-qualified-names.txt";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    // Fallback to empty array if resource not found
                    return new string[0];
                }
                
                using (var reader = new StreamReader(stream))
                {
                    var names = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            names.Add(line);
                        }
                    }
                    return names.ToArray();
                }
            }
        }

        private static void VerifyNamesSorted(string[] names)
        {
            for (int i = 1; i < names.Length; i++)
            {
                if (StringComparer.Ordinal.Compare(names[i - 1], names[i]) > 0)
                {
                    throw new InvalidOperationException($"Reserved type names file is not sorted. '{names[i - 1]}' comes before '{names[i]}'");
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

            // Check for conflicts with reserved types
            int index = Array.BinarySearch(ReservedTypeNames, typeName, StringComparer.Ordinal);
            if (index >= 0)
            {
                var qualifiedTypeName = index < QualifiedTypeNames.Length ? QualifiedTypeNames[index] : "unknown type";
                
                // Verify that the qualified name corresponds to the same type name with proper casing
                var lastDotIndex = qualifiedTypeName.LastIndexOf('.');
                var extractedTypeName = lastDotIndex >= 0 ? qualifiedTypeName.Substring(lastDotIndex + 1) : qualifiedTypeName;
                if (!string.Equals(typeName, extractedTypeName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Type name mismatch: expected '{typeName}' but qualified name contains '{extractedTypeName}' at index {index}");
                }
                
                foreach (var location in namedTypeSymbol.Locations)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0034, location, typeName, qualifiedTypeName), namedTypeSymbol);
                }
            }
        }
    }
}