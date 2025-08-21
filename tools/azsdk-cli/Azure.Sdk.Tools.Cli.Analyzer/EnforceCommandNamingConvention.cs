using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnforceCommandNamingConventionAnalyzer : DiagnosticAnalyzer
    {
        public const string Id = "MCP004";
        private static readonly DiagnosticDescriptor commandRule = new DiagnosticDescriptor(
            Id,
            "CLI command names must follow kebab-case convention",
            "Command name '{0}' must follow kebab-case convention (lowercase letters, numbers, and hyphens only)",
            "Naming",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public const string OptionId = "MCP005";
        private static readonly DiagnosticDescriptor optionRule = new DiagnosticDescriptor(
            OptionId,
            "CLI option names must follow kebab-case convention",
            "Option name '{0}' must follow kebab-case convention (lowercase letters, numbers, and hyphens only)",
            "Naming",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        // Kebab-case pattern: lowercase letters/numbers, separated by hyphens, no consecutive hyphens
        private static readonly Regex kebabCasePattern = new Regex(@"^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(commandRule, optionRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

            var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation);
            var typeName = typeInfo.Type?.Name;

            if (typeName == "Command")
            {
                AnalyzeCommandNaming(context, objectCreation);
            }
            else if (typeName == "Option")
            {
                AnalyzeOptionNaming(context, objectCreation);
            }
        }

        private static void AnalyzeCommandNaming(SyntaxNodeAnalysisContext context, ObjectCreationExpressionSyntax objectCreation)
        {
            // Get the first argument (command name)
            if (objectCreation.ArgumentList?.Arguments.Count > 0)
            {
                var firstArgument = objectCreation.ArgumentList.Arguments[0];

                // Check if the argument is a string literal
                if (firstArgument.Expression is LiteralExpressionSyntax literal &&
                    literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                {
                    var commandName = literal.Token.ValueText;

                    // Validate kebab-case convention
                    if (!kebabCasePattern.IsMatch(commandName))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            commandRule,
                            literal.GetLocation(),
                            commandName));
                    }
                }
            }
        }

        private static void AnalyzeOptionNaming(SyntaxNodeAnalysisContext context, ObjectCreationExpressionSyntax objectCreation)
        {
            // Get the first argument (option names array)
            if (objectCreation.ArgumentList?.Arguments.Count > 0)
            {
                var firstArgument = objectCreation.ArgumentList.Arguments[0];

                // Check if the argument is an array creation expression or collection expression
                if (firstArgument.Expression is ArrayCreationExpressionSyntax arrayCreation)
                {
                    AnalyzeArrayCreationExpression(context, arrayCreation.Initializer);
                }
                else if (firstArgument.Expression is ImplicitArrayCreationExpressionSyntax implicitArray)
                {
                    AnalyzeArrayCreationExpression(context, implicitArray.Initializer);
                }
                else if (firstArgument.Expression is CollectionExpressionSyntax collectionExpression)
                {
                    AnalyzeCollectionExpression(context, collectionExpression);
                }
            }
        }

        private static void AnalyzeArrayCreationExpression(SyntaxNodeAnalysisContext context, InitializerExpressionSyntax initializer)
        {
            if (initializer?.Expressions == null)
            {
                return;
            }

            foreach (var expression in initializer.Expressions)
            {
                if (expression is LiteralExpressionSyntax literal &&
                    literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                {
                    ValidateOptionName(context, literal);
                }
            }
        }

        private static void AnalyzeCollectionExpression(SyntaxNodeAnalysisContext context, CollectionExpressionSyntax collectionExpression)
        {
            foreach (var element in collectionExpression.Elements)
            {
                if (element is ExpressionElementSyntax expressionElement &&
                    expressionElement.Expression is LiteralExpressionSyntax literal &&
                    literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                {
                    ValidateOptionName(context, literal);
                }
            }
        }

        private static void ValidateOptionName(SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literal)
        {
            var optionName = literal.Token.ValueText;

            // Only validate long options (starting with --), skip short options like -p
            if (optionName.StartsWith("--"))
            {
                // Remove the -- prefix for validation
                var nameWithoutPrefix = optionName.Substring(2);

                // Validate kebab-case convention
                if (!kebabCasePattern.IsMatch(nameWithoutPrefix))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        optionRule,
                        literal.GetLocation(),
                        optionName));
                }
            }
        }
    }
}
