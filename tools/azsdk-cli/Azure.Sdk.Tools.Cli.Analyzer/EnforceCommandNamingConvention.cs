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
        public const string Id = "MCP003";
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            Id,
            "CLI command names must follow kebab-case convention",
            "Command name '{0}' must follow kebab-case convention (lowercase letters, numbers, and hyphens only)",
            "Naming",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        // Kebab-case pattern: lowercase letters/numbers, separated by hyphens, no consecutive hyphens
        private static readonly Regex KebabCasePattern = new Regex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

            // Check if this is a Command constructor
            var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation);
            if (typeInfo.Type?.Name != "Command")
            {
                return;
            }

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
                    if (!KebabCasePattern.IsMatch(commandName))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Rule,
                            literal.GetLocation(),
                            commandName));
                    }
                }
            }
        }
    }
}