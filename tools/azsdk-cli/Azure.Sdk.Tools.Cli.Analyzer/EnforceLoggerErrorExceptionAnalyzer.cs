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
    public sealed class EnforceLoggerErrorExceptionAnalyzer : DiagnosticAnalyzer
    {
        public const string Id = "MCP009";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            Id,
            "logger.LogError calls must include an exception argument",
            "Call '{0}' with an exception as the first argument",
            "Reliability",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            if (!(context.Node is InvocationExpressionSyntax invocation))
            {
                return;
            }

            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
            {
                return;
            }

            if (!string.Equals(memberAccess.Name.Identifier.Text, "LogError", StringComparison.Ordinal))
            {
                return;
            }

            var catchClause = invocation.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();
            if (catchClause == null)
            {
                return;
            }

            var catchDeclaration = catchClause.Declaration;
            if (catchDeclaration == null || catchDeclaration.Identifier.IsKind(SyntaxKind.None))
            {
                return;
            }

            var catchSymbol = context.SemanticModel.GetDeclaredSymbol(catchDeclaration) as ILocalSymbol;
            if (catchSymbol == null)
            {
                return;
            }


            if (invocation.ArgumentList?.Arguments.Count == 0)
            {
                return;
            }

            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            var methodSymbol = GetMethodSymbol(symbolInfo);
            if (methodSymbol == null)
            {
                return;
            }

            if (!IsLoggingMethod(methodSymbol))
            {
                return;
            }

            if (invocation.ArgumentList.Arguments.Count == 0)
            {
                return;
            }

            var firstArgument = invocation.ArgumentList.Arguments[0];
            if (!IsCatchVariable(firstArgument.Expression, catchSymbol, context.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), memberAccess.ToString()));
            }
        }

        private static IMethodSymbol GetMethodSymbol(SymbolInfo symbolInfo)
        {
            if (symbolInfo.Symbol is IMethodSymbol method)
            {
                return method;
            }

            return symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        }

        private static bool IsLoggingMethod(IMethodSymbol methodSymbol)
        {
            var symbol = methodSymbol?.ReducedFrom ?? methodSymbol;
            if (!string.Equals(symbol?.Name, "LogError", StringComparison.Ordinal))
            {
                return false;
            }

            var containingType = symbol?.ContainingType;
            if (containingType == null)
            {
                return false;
            }

            var namespaceName = containingType.ContainingNamespace?.ToDisplayString();
            return string.Equals(namespaceName, "Microsoft.Extensions.Logging", StringComparison.Ordinal);
        }

        private static bool IsCatchVariable(ExpressionSyntax expression, ILocalSymbol catchSymbol, SemanticModel semanticModel)
        {
            var unwrapped = Unwrap(expression);
            var symbol = semanticModel.GetSymbolInfo(unwrapped).Symbol;
            if (symbol != null)
            {
                return SymbolEqualityComparer.Default.Equals(symbol, catchSymbol);
            }

            if (unwrapped is IdentifierNameSyntax identifier)
            {
                return string.Equals(identifier.Identifier.ValueText, catchSymbol.Name, StringComparison.Ordinal);
            }

            return false;
        }

        private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
        {
            while (true)
            {
                switch (expression)
                {
                    case ParenthesizedExpressionSyntax parenthesized:
                        expression = parenthesized.Expression;
                        continue;
                    case PostfixUnaryExpressionSyntax postfix when postfix.OperatorToken.IsKind(SyntaxKind.ExclamationToken):
                        expression = postfix.Operand;
                        continue;
                    default:
                        return expression;
                }
            }
        }
    }
}
