using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BannedTypesAnalyzer : SymbolAnalyzerBase
    {
        private static HashSet<string> BannedTypes = new HashSet<string>()
        {
            "MutableJsonDocument",
            "MutableJsonElement",
            "MutableJsonChange"
        };

        private static readonly string BannedTypesMessageArgs = string.Join(", ", BannedTypes);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.AZC0020);

        public override SymbolKind[] SymbolKinds { get; } = new[]
        {
            SymbolKind.Method,
            SymbolKind.Field,
            SymbolKind.Property,
            SymbolKind.Parameter,
            SymbolKind.Event,
            SymbolKind.NamedType
        };

        public override void Analyze(ISymbolAnalysisContext context)
        {
            static void CheckType(ISymbolAnalysisContext context, ITypeSymbol type, ISymbol symbol)
            {
                if (type is INamedTypeSymbol namedTypeSymbol)
                {
                    if (BannedTypes.Contains(type.Name))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0020, symbol.Locations.First(), BannedTypesMessageArgs), symbol);
                    }

                    if (namedTypeSymbol.IsGenericType)
                    {
                        foreach (var typeArgument in namedTypeSymbol.TypeArguments)
                        {
                            CheckType(context, typeArgument, symbol);
                        }
                    }
                }
            }

            switch (context.Symbol)
            {
                case IParameterSymbol parameterSymbol:
                    CheckType(context, parameterSymbol.Type, parameterSymbol);
                    break;
                case IMethodSymbol methodSymbol:
                    CheckType(context, methodSymbol.ReturnType, methodSymbol);
                    break;
                case IEventSymbol eventSymbol:
                    CheckType(context, eventSymbol.Type, eventSymbol);
                    break;
                case IPropertySymbol propertySymbol:
                    CheckType(context, propertySymbol.Type, propertySymbol);
                    break;
                case IFieldSymbol fieldSymbol:
                    CheckType(context, fieldSymbol.Type, fieldSymbol);
                    break;
                case INamedTypeSymbol namedTypeSymbol:
                    CheckType(context, namedTypeSymbol.BaseType, namedTypeSymbol);
                    foreach (var iface in namedTypeSymbol.Interfaces)
                    {
                        CheckType(context, iface, namedTypeSymbol);
                    }
                    break;
            }
        }
    }
}
