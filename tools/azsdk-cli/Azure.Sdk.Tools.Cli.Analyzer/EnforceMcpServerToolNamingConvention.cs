using System.Collections.Immutable;
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
        public const string MissingNameId = "MCP006";
        public const string InvalidNamingId = "MCP007";
        public const string MissingAzsdkPrefixId = "MCP008";

        public const string MCP_ATTRIBUTE_NAME = "McpServerTool";
        public const string AZSDK_PREFIX = "azsdk_";

        private static readonly DiagnosticDescriptor missingNameRule = new DiagnosticDescriptor(
            MissingNameId,
            MCP_ATTRIBUTE_NAME + " attribute must specify a Name property",
            MCP_ATTRIBUTE_NAME + " attribute must include Name property with snake_case convention",
            "Naming",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        // NOTE: do not use interpolated strings here, otherwise the '{0}' will not be filled in for compiler messages
        private static readonly DiagnosticDescriptor invalidNamingRule = new DiagnosticDescriptor(
            InvalidNamingId,
            MCP_ATTRIBUTE_NAME + " attribute parameter 'Name' must follow snake_case convention",
            MCP_ATTRIBUTE_NAME + " Name '{0}' must follow snake_case convention (lowercase letters, numbers, and underscores only)",
            "Naming",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        // NOTE: do not use interpolated strings here, otherwise the '{0}' will not be filled in for compiler messages
        private static readonly DiagnosticDescriptor missingPrefixRule = new DiagnosticDescriptor(
            MissingAzsdkPrefixId,
            MCP_ATTRIBUTE_NAME + " attribute parameter 'Name' must start with '" + AZSDK_PREFIX + "'",
            MCP_ATTRIBUTE_NAME + " Name '{0}' must start with '" + AZSDK_PREFIX + "'",
            "Naming",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        // Snake-case pattern: lowercase letters/numbers, separated by underscores, no consecutive underscores
        private static readonly Regex snakeCasePattern = new Regex(@"^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", RegexOptions.Compiled);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(missingNameRule, invalidNamingRule, missingPrefixRule);

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
            if (attribute.Name.ToString() != MCP_ATTRIBUTE_NAME)
            {
                return;
            }

            // Check if the attribute has arguments
            if (attribute.ArgumentList?.Arguments.Count == 0 || attribute.ArgumentList == null)
            {
                // Missing Name property
                context.ReportDiagnostic(Diagnostic.Create(
                    missingNameRule,
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

                        // Enforce azsdk prefix
                        if (!toolName.StartsWith(AZSDK_PREFIX))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                missingPrefixRule,
                                literal.GetLocation(),
                                toolName));
                        }

                        // Validate snake_case convention
                        if (!snakeCasePattern.IsMatch(toolName))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                invalidNamingRule,
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
                    missingNameRule,
                    attribute.GetLocation()));
            }
        }
    }
}
