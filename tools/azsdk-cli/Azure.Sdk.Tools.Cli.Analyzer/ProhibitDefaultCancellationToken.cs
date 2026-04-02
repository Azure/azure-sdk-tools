using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ProhibitDefaultCancellationTokenAnalyzer : DiagnosticAnalyzer
    {
        public const string Id = "AZSDK002";
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            Id,
            "Do not pass 'default' or 'CancellationToken.None' as a CancellationToken argument",
            "Do not pass '{0}' as a CancellationToken argument; forward an existing token instead",
            "Reliability",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly string[] TestFrameworkAssemblies =
        {
            "nunit.framework",
            "xunit.core",
            "xunit.assert",
            "Microsoft.VisualStudio.TestPlatform.TestFramework"
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeArgument, SyntaxKind.Argument);
        }

        private static bool IsTestProject(Compilation compilation)
        {
            return compilation.ReferencedAssemblyNames.Any(a =>
                TestFrameworkAssemblies.Any(t =>
                    string.Equals(a.Name, t, System.StringComparison.OrdinalIgnoreCase)));
        }

        private static void AnalyzeArgument(SyntaxNodeAnalysisContext context)
        {
            var argument = (ArgumentSyntax)context.Node;
            var expression = argument.Expression;

            // Check for CancellationToken.None
            if (expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "None")
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken);
                if (typeInfo.Type?.ToDisplayString() == "System.Threading.CancellationToken" &&
                    !IsTestProject(context.SemanticModel.Compilation))
                {
                    var diagnostic = Diagnostic.Create(Rule, argument.GetLocation(), "CancellationToken.None");
                    context.ReportDiagnostic(diagnostic);
                }
                return;
            }

            // Check for `default` literal
            if (expression.IsKind(SyntaxKind.DefaultLiteralExpression))
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken);
                if (typeInfo.ConvertedType?.ToDisplayString() == "System.Threading.CancellationToken" &&
                    !IsTestProject(context.SemanticModel.Compilation))
                {
                    var diagnostic = Diagnostic.Create(Rule, argument.GetLocation(), "default");
                    context.ReportDiagnostic(diagnostic);
                }
                return;
            }

            // Check for `default(CancellationToken)`
            if (expression is DefaultExpressionSyntax)
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken);
                if (typeInfo.Type?.ToDisplayString() == "System.Threading.CancellationToken" &&
                    !IsTestProject(context.SemanticModel.Compilation))
                {
                    var diagnostic = Diagnostic.Create(Rule, argument.GetLocation(), expression.ToString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
