using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MCPAnalyzer : DiagnosticAnalyzer
    {
        public const string Id = "MCP001";
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            Id,
            "McpServerTool methods must wrap body in try/catch, see the README within the tools directory for examples",
            "Method '{0}' must have its entire body inside try {{ }} catch(Exception)",
            "Reliability",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext ctx)
        {
            ctx.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            ctx.EnableConcurrentExecution();
            ctx.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        /// <summary>
        /// This is the main bulk of this analyzer. It scans for methods that are decorated with MCPServerTool attribute,
        /// and ensures those checks properly wrap their body in a try/catch block. This catch should be a generalized System.Exception.
        /// Within the catch block, there must be a SetFailure() call.
        /// </summary>
        /// <param name="ctx"></param>
        private static void AnalyzeMethod(SyntaxNodeAnalysisContext ctx)
        {
            var md = (MethodDeclarationSyntax)ctx.Node;

            // 1) does this method have [McpServerTool]
            bool hasAttr = md.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(a => a.Name.ToString().Contains("McpServerTool"));

            if (!hasAttr)
                return;

            var body = md.Body;
            if (body == null)
                return;

            // check that there is one statement surrounding the body. todo: is this too restrictive?
            var stmts = body.Statements;
            if (stmts.Count != 1 || !(stmts[0] is TryStatementSyntax tryStmt))
            {
                Report();
                return;
            }

            // verify there’s a catch(Exception)
            bool hasExCatch = tryStmt.Catches
                .Any(c => c.Declaration?.Type.ToString() == "Exception");
            if (!hasExCatch)
                Report();

            void Report() => ctx.ReportDiagnostic(
                Diagnostic.Create(Rule, md.Identifier.GetLocation(), md.Identifier.Text));
        }
    }
}
