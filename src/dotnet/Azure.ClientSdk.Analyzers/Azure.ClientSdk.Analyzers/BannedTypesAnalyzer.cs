using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.ClientSdk.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BannedTypesAnalyzer : DiagnosticAnalyzer
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

        public SymbolKind[] SymbolKinds { get; } = new[]
        {
            SymbolKind.Event,
            SymbolKind.Field,
            SymbolKind.Method,
            SymbolKind.NamedType,
            SymbolKind.Parameter,
            SymbolKind.Property,
        };

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(c => Analyze(c), SymbolKinds);
            context.RegisterSyntaxNodeAction(c => AnalyzeNode(c), SyntaxKind.LocalDeclarationStatement);
        }

        public void Analyze(SymbolAnalysisContext context)
        {
            if (IsAzureCore(context.Symbol.ContainingAssembly))
            {
                return;
            }

            switch (context.Symbol)
            {
                case IParameterSymbol parameterSymbol:
                    CheckType(parameterSymbol.Type, parameterSymbol, context.ReportDiagnostic);
                    break;
                case IMethodSymbol methodSymbol:
                    CheckType(methodSymbol.ReturnType, methodSymbol, context.ReportDiagnostic);
                    break;
                case IEventSymbol eventSymbol:
                    CheckType(eventSymbol.Type, eventSymbol, context.ReportDiagnostic);
                    break;
                case IPropertySymbol propertySymbol:
                    CheckType(propertySymbol.Type, propertySymbol, context.ReportDiagnostic);
                    break;
                case IFieldSymbol fieldSymbol:
                    CheckType(fieldSymbol.Type, fieldSymbol, context.ReportDiagnostic);
                    break;
                case INamedTypeSymbol namedTypeSymbol:
                    CheckType(namedTypeSymbol.BaseType, namedTypeSymbol, context.ReportDiagnostic);
                    foreach (var iface in namedTypeSymbol.Interfaces)
                    {
                        CheckType(iface, namedTypeSymbol, context.ReportDiagnostic);
                    }
                    break;
            }
        }

        public void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            if (IsAzureCore(context.ContainingSymbol.ContainingAssembly))
            {
                return;
            }

            if (context.Node is LocalDeclarationStatementSyntax declaration)
            {
                ITypeSymbol type = context.SemanticModel.GetTypeInfo(declaration.Declaration.Type).Type;

                CheckType(type, type, context.ReportDiagnostic, context.Node.GetLocation());
            }
        }

        private static Diagnostic CheckType(ITypeSymbol type, ISymbol symbol, Action<Diagnostic> reportDiagnostic, Location location = default)
        {
            if (type is INamedTypeSymbol namedTypeSymbol)
            {
                if (IsBannedType(namedTypeSymbol))
                {
                    reportDiagnostic(Diagnostic.Create(Descriptors.AZC0020, location ?? symbol.Locations.First(), BannedTypesMessageArgs));
                }

                if (namedTypeSymbol.IsGenericType)
                {
                    foreach (var typeArgument in namedTypeSymbol.TypeArguments)
                    {
                        CheckType(typeArgument, symbol, reportDiagnostic);
                    }
                }
            }

            return null;
        }

        private static bool IsAzureCore(IAssemblySymbol assembly)
        {
            return
                assembly.Name.Equals("Azure.Core") ||
                assembly.Name.Equals("Azure.Core.Experimental");
        }

        private static bool IsBannedType(INamedTypeSymbol namedTypeSymbol)
        {
            return BannedTypes.Contains($"{namedTypeSymbol.ContainingNamespace}.{namedTypeSymbol.Name}");
        }
    }
}
