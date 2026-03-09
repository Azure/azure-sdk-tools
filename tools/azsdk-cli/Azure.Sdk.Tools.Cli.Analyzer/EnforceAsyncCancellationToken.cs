using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnforceAsyncCancellationTokenAnalyzer : DiagnosticAnalyzer
    {
        public const string Id = "AZSDK001";
        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            Id,
            "Async methods must accept a CancellationToken parameter",
            "Async method '{0}' must accept a CancellationToken parameter",
            "Reliability",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            if (!(semanticModel.GetDeclaredSymbol(methodDeclaration) is IMethodSymbol methodSymbol))
            {
                return;
            }

            // Only analyze public or internal methods
            if (methodSymbol.DeclaredAccessibility != Accessibility.Public &&
                methodSymbol.DeclaredAccessibility != Accessibility.Internal)
            {
                return;
            }

            // Skip overrides and interface implementations — their signature is dictated by the base/interface
            if (methodSymbol.IsOverride)
            {
                return;
            }

            // Skip explicit interface implementations
            if (methodSymbol.ExplicitInterfaceImplementations.Length > 0)
            {
                return;
            }

            // Skip Main entry points
            if (methodSymbol.Name == "Main" && methodSymbol.IsStatic)
            {
                return;
            }

            // Must return a Task-like type
            if (!ReturnsTaskType(methodSymbol.ReturnType))
            {
                return;
            }

            // Skip test methods
            if (HasTestAttribute(methodDeclaration))
            {
                return;
            }

            // Check if method already has a CancellationToken parameter
            if (HasCancellationTokenParameter(methodSymbol))
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool ReturnsTaskType(ITypeSymbol returnType)
        {
            if (returnType is INamedTypeSymbol namedType)
            {
                var fullName = namedType.ConstructedFrom?.ToDisplayString() ?? namedType.ToDisplayString();

                return fullName == "System.Threading.Tasks.Task" ||
                       fullName == "System.Threading.Tasks.Task<TResult>" ||
                       fullName == "System.Threading.Tasks.ValueTask" ||
                       fullName == "System.Threading.Tasks.ValueTask<TResult>";
            }

            return false;
        }

        private static bool HasTestAttribute(MethodDeclarationSyntax methodDeclaration)
        {
            var testAttributeNames = new[] { "Test", "TestMethod", "Fact", "Theory" };

            return methodDeclaration.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(a =>
                {
                    var name = a.Name.ToString();
                    return testAttributeNames.Any(t =>
                        name == t || name == t + "Attribute");
                });
        }

        private static bool HasCancellationTokenParameter(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Any(p =>
                p.Type.ToDisplayString() == "System.Threading.CancellationToken");
        }
    }
}
