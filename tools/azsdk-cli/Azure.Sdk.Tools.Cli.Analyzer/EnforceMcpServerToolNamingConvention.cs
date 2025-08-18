using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Azure.Sdk.Tools.Cli.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnforceMcpServerToolNamingConventionAnalyzer : DiagnosticAnalyzer
    {
        public const string MissingNameId = "MCP004";
        public const string InvalidNamingId = "MCP005";

        private static readonly DiagnosticDescriptor MissingNameRule = new DiagnosticDescriptor(
            MissingNameId,
            "McpServerTool attribute must specify a Name property",
            "McpServerTool attribute must include Name property with snake_case convention",
            "Naming",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor InvalidNamingRule = new DiagnosticDescriptor(
            InvalidNamingId,
            "McpServerTool Name must follow snake_case convention",
            "McpServerTool Name '{0}' must follow snake_case convention (lowercase letters, numbers, and underscores only)",
            "Naming",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // Snake-case pattern: lowercase letters/numbers, separated by underscores, no consecutive underscores
        private static readonly Regex SnakeCasePattern = new Regex(@"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(MissingNameRule, InvalidNamingRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
        }

        private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
        {
            var attribute = (AttributeSyntax)context.Node;

            // Check if this is McpServerTool attribute
            if (!attribute.Name.ToString().Contains("McpServerTool"))
            {
                return;
            }

            // Check if the attribute has arguments
            if (attribute.ArgumentList?.Arguments.Count == 0 || attribute.ArgumentList == null)
            {
                // Missing Name property
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingNameRule,
                    attribute.GetLocation()));
                return;
            }

            // Look for Name property assignment
            bool hasName = false;
            foreach (var argument in attribute.ArgumentList.Arguments)
            {
                if (argument.NameEquals?.Name.Identifier.ValueText == "Name")
                {
                    hasName = true;

                    // Check if the value is a string literal
                    if (argument.Expression is LiteralExpressionSyntax literal &&
                        literal.Token.IsKind(SyntaxKind.StringLiteralToken))
                    {
                        var toolName = literal.Token.ValueText;

                        // Validate snake_case convention
                        if (!SnakeCasePattern.IsMatch(toolName))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                InvalidNamingRule,
                                literal.GetLocation(),
                                toolName));
                        }
                    }
                    break;
                }
            }

            if (!hasName)
            {
                // Name property not found
                context.ReportDiagnostic(Diagnostic.Create(
                    MissingNameRule,
                    attribute.GetLocation()));
            }
        }
    }
}
