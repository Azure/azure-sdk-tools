using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BannedTypesAnalyzer : SymbolAnalyzerBase
    {
        private static HashSet<string> BannedTypes = new HashSet<string>()
        {
            "Azure.Core.Json.MutableJsonDocument",
            "Azure.Core.Json.MutableJsonElement",
            "Azure.Core.Json.MutableJsonChange",
            "Azure.Core.Json.MutableJsonChangeKind",
        };

        private static readonly string BannedTypesMessageArgs = string.Join(", ", BannedTypes);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.AZC0020);

        public override SymbolKind[] SymbolKinds { get; } = new[]
        {
            SymbolKind.Event,
            SymbolKind.Field,
            SymbolKind.Local,
            SymbolKind.Method,
            SymbolKind.NamedType,
            SymbolKind.Parameter,
            SymbolKind.Property,
        };

        // Note: suppressing warnings because they are handled in base.Initialize().
#pragma warning disable RS1025 // Configure generated code analysis
#pragma warning disable RS1026 // Enable concurrent execution
        public override void Initialize(AnalysisContext context)
#pragma warning restore RS1026 // Enable concurrent execution
#pragma warning restore RS1025 // Configure generated code analysis
        {
            base.Initialize(context);

            context.RegisterSyntaxNodeAction(c => AnalyzeNode(c), SyntaxKind.LocalDeclarationStatement);
        }

        public override void Analyze(ISymbolAnalysisContext context)
        {
            Debug.WriteLine($"{context.Symbol}");

            if (IsAzureCore(context.Symbol.ContainingAssembly))
            {
                return;
            }

            switch (context.Symbol)
            {
                case IParameterSymbol parameterSymbol:
                    CheckType(context, parameterSymbol.Type, parameterSymbol);
                    break;
                case IMethodSymbol methodSymbol:
                    CheckType(context, methodSymbol.ReturnType, methodSymbol);

                    //foreach (var typeSymbol in methodSymbol.)
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
                case ILocalSymbol localSymbol:
                    CheckType(context, localSymbol.Type, localSymbol);
                    break;
                case INamedTypeSymbol namedTypeSymbol:
                    CheckType(context, namedTypeSymbol.BaseType, namedTypeSymbol);
                    foreach (var iface in namedTypeSymbol.Interfaces)
                    {
                        CheckType(context, iface, namedTypeSymbol);
                    }
                    break;
            }

            Debug.WriteLine($"done");
        }

        public void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            Debug.WriteLine($"{context.Node}");

            if (IsAzureCore(context.ContainingSymbol.ContainingAssembly))
            {
                return;
            }

            if (context.Node is LocalDeclarationStatementSyntax declaration)
            {
                ITypeSymbol type = context.SemanticModel.GetTypeInfo(declaration.Declaration.Type).Type;

                if (type is INamedTypeSymbol namedTypeSymbol)
                {
                    if (IsBannedType(namedTypeSymbol))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.AZC0020, context.Node.GetLocation(), BannedTypesMessageArgs));
                    }
                }
            }
        }

        private static void CheckType(ISymbolAnalysisContext context, ITypeSymbol type, ISymbol symbol)
        {
            if (type is INamedTypeSymbol namedTypeSymbol)
            {
                if (IsBannedType(namedTypeSymbol))
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

        private static bool IsBannedType(INamedTypeSymbol namedTypeSymbol)
        {
            return BannedTypes.Contains($"{namedTypeSymbol.ContainingNamespace}.{namedTypeSymbol.Name}");
        }

        private static bool IsAzureCore(IAssemblySymbol assembly)
        {
            return
                assembly.Name.Equals("Azure.Core") ||
                assembly.Name.Equals("Azure.Core.Experimental");
        }
    }
}
