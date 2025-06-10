// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DuplicateTypeNameAnalyzer : SymbolAnalyzerBase
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.AZC0034);
        public override SymbolKind[] SymbolKinds { get; } = { SymbolKind.NamedType };

        // Common .NET platform type names that Azure SDK types should avoid conflicting with
        private static readonly HashSet<string> PlatformTypeNames = new HashSet<string>
        {
            // Common System types
            "Object", "String", "Boolean", "Char", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64",
            "Single", "Double", "Decimal", "DateTime", "DateTimeOffset", "TimeSpan", "Guid", "Uri", "Version", "Type",
            // Common collection types
            "Array", "List", "Dictionary", "HashSet", "Queue", "Stack", "Collection", "Enumerable", "Enumerator",
            // Common exception types
            "Exception", "ArgumentException", "ArgumentNullException", "InvalidOperationException", "NotSupportedException",
            "NotImplementedException", "FormatException", "OverflowException", "OutOfMemoryException",
            // Common async types
            "Task", "ValueTask", "CancellationToken", "CancellationTokenSource",
            // Common interface types
            "IDisposable", "IComparable", "IEquatable", "IEnumerable", "ICollection", "IList", "IDictionary",
            // Common attribute types
            "Attribute", "ObsoleteAttribute", "SerializableAttribute",
            // Other common types
            "Console", "Environment", "Math", "Random", "Buffer", "Convert", "Encoding", "Stream", "TextReader", "TextWriter"
        };

        // Allowed nested type names that are standard across Azure SDK
        private static readonly HashSet<string> AllowedNestedTypeNames = new HashSet<string>
        {
            "ServiceVersion",
            "Enumerator"
        };

        // Names that should only be used as nested types in Azure SDK
        private static readonly HashSet<string> NestedOnlyTypeNames = new HashSet<string>
        {
            "ServiceVersion",
            "Enumerator"
        };

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
            if (namedTypeSymbol.ContainingType != null && AllowedNestedTypeNames.Contains(typeName))
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
            if (PlatformTypeNames.Contains(typeName))
            {
                foreach (var location in namedTypeSymbol.Locations)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0034, location, typeName), namedTypeSymbol);
                }
            }
        }
    }
}