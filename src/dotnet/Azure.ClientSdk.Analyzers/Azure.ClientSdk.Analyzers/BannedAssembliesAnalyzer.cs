using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Azure.ClientSdk.Analyzers
{
    public sealed class BannedAssembliesAnalyzer : SymbolAnalyzerBase
    {
        private static HashSet<string> BannedAssemblies = new HashSet<string>()
        {
            "System.Text.Json",
            "Newtonsoft.Json",
            "System.Collections.Immutable"
        };

        private static readonly string BannedAssembliesMessageArgs = string.Join(", ", BannedAssemblies);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.AZC0014);

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
            void CheckType(ITypeSymbol type, ISymbol symbol)
            {
                if (type is INamedTypeSymbol namedTypeSymbol)
                {
                    if (BannedAssemblies.Contains(type.ContainingAssembly.Name))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0014, symbol.Locations.First(), BannedAssembliesMessageArgs), symbol);
                    }

                    if (namedTypeSymbol.IsGenericType)
                    {
                        foreach (var typeArgument in namedTypeSymbol.TypeArguments)
                        {
                            CheckType(typeArgument, symbol);
                        }
                    }
                }
            }

            if (!IsPublicApi(context.Symbol))
            {
                return;
            }

            switch (context.Symbol)
            {
                case IParameterSymbol parameterSymbol:
                    CheckType(parameterSymbol.Type, parameterSymbol);
                    break;
                case IMethodSymbol methodSymbol:
                    if (methodSymbol.MethodKind == MethodKind.PropertyGet || methodSymbol.MethodKind == MethodKind.PropertySet)
                    {
                        return;
                    }
                    CheckType(methodSymbol.ReturnType, methodSymbol);
                    break;
                case IEventSymbol eventSymbol:
                    CheckType(eventSymbol.Type, eventSymbol);
                    break;
                case IPropertySymbol propertySymbol:
                    CheckType(propertySymbol.Type, propertySymbol);
                    break;
                case IFieldSymbol fieldSymbol:
                    CheckType(fieldSymbol.Type, fieldSymbol);
                    break;
                case INamedTypeSymbol namedTypeSymbol:
                    CheckType(namedTypeSymbol.BaseType, namedTypeSymbol);
                    foreach (var iface in namedTypeSymbol.Interfaces)
                    {
                        CheckType(iface, namedTypeSymbol);
                    }
                    break;
            }
        }
    }
}