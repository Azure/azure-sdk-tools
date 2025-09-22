using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnforceToolsListAnalyzer : DiagnosticAnalyzer
    {
        public const string Id = "MCP002";
        private static readonly DiagnosticDescriptor rule = new DiagnosticDescriptor(
            Id,
            "Every MCPTool must be listed in SharedOptions.ToolsList",
            "MCPTool implementation '{0}' is not included in SharedOptions.ToolsList",
            "Reliability",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: new[] { WellKnownDiagnosticTags.CompilationEnd }
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(startCtx =>
            {
                // These will be populated during the compilation
                var listedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                var implementations = new List<INamedTypeSymbol>();

                // 1) Collect every typeof(...) inside SharedOptions.ToolsList
                startCtx.RegisterSyntaxNodeAction(syntaxCtx =>
                {
                    var tof = (TypeOfExpressionSyntax)syntaxCtx.Node;

                    // Find the field symbol this typeof lives in
                    var variable = tof.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
                    if (variable == null)
                    {
                        return;
                    }

                    if (!(syntaxCtx.SemanticModel.GetDeclaredSymbol(variable) is IFieldSymbol field))
                    {
                        return;
                    }

                    // Check it's the exact static List<Type> ToolsList on your SharedOptions
                    if (field.ContainingType?.ToDisplayString() == "Azure.Sdk.Tools.Cli.Commands.SharedOptions"
                     && field.Name == "ToolsList")
                    {
                        if (syntaxCtx.SemanticModel.GetTypeInfo(tof.Type).Type is INamedTypeSymbol sym)
                        {
                            listedTypes.Add(sym);
                        }
                    }
                }, SyntaxKind.TypeOfExpression);

                // 2) Collect every non-abstract class inheriting MCPTool
                startCtx.RegisterSymbolAction(symCtx =>
                {
                    var named = (INamedTypeSymbol)symCtx.Symbol;
                    if (named.TypeKind != TypeKind.Class || named.IsAbstract)
                    {
                        return;
                    }

                    var baseTool = symCtx.Compilation.GetTypeByMetadataName("Azure.Sdk.Tools.Cli.Contract.MCPToolBase");
                    if (baseTool != null && InheritsFrom(named, baseTool))
                    {
                        implementations.Add(named);
                    }

                }, SymbolKind.NamedType);

                // 3) At compilation end, compare and report any missing ones
                startCtx.RegisterCompilationEndAction(endCtx =>
                {
                    foreach (var impl in implementations)
                    {
                        if (!listedTypes.Contains(impl))
                        {
                            // Report on the location of the class declaration
                            var loc = impl.Locations.FirstOrDefault() ?? Location.None;
                            endCtx.ReportDiagnostic(Diagnostic.Create(rule, loc, impl.Name));
                        }
                    }
                });
            });
        }

        private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (var t = type.BaseType; t != null; t = t.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(t, baseType))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
