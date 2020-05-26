using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
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
            static void CheckType(ISymbolAnalysisContext context, ITypeSymbol type, ISymbol symbol)
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
                            CheckType(context, typeArgument, symbol);
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
                    CheckType(context, parameterSymbol.Type, parameterSymbol);
                    break;
                case IMethodSymbol methodSymbol:
                    if (methodSymbol.MethodKind == MethodKind.PropertyGet || methodSymbol.MethodKind == MethodKind.PropertySet)
                    {
                        return;
                    }
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