// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// General rule prompts for Azure SDK general analyzers.
    /// </summary>
    internal static partial class AnalyzerPrompts
    {
        /// <summary>
        /// Adds general rule prompts to the builder.
        /// Includes fallback mechanisms for unknown error types.
        /// </summary>
        static void AddGeneralPrompts(Dictionary<string, AgentPromptFix> builder)
        {
            // Fallback prompt for unknown/unhandled errors from any source
            builder["__FALLBACK__"] = new AgentPromptFix(
                prompt: """
                TASK: Analyze and Fix Unknown Error

                You are analyzing an error that doesn't have a specific handler. This could be from various sources including:
                • TypeSpec compiler errors
                • .NET compiler errors (C#/VB.NET)
                • Roslyn analyzer errors
                • MSBuild errors
                • Custom analyzer errors
                • Azure SDK specific errors

                ANALYSIS APPROACH:
                1. IDENTIFY ERROR SOURCE:
                   - Look at the error code pattern to determine the source:
                     * TypeSpec: Usually starts with letters (e.g., "invalid-template", "duplicate-declaration")
                     * C# Compiler: Usually CS#### format (e.g., "CS0103", "CS1002")
                     * MSBuild: Usually MSB#### format (e.g., "MSB3644", "MSB4181")
                     * Analyzer: Usually custom prefix + numbers (e.g., "AZC####", "CA####", "IDE####")
                     * Azure SDK: May have AZC, ARM, or service-specific prefixes

                2. UNDERSTAND THE ERROR:
                   - Read the error message carefully to understand what's wrong
                   - Identify the file, line, or symbol mentioned in the error
                   - Determine if it's a syntax error, semantic error, or policy violation

                3. PROVIDE TARGETED SOLUTION:
                   For TypeSpec Errors:
                   - Check for syntax issues in .tsp files
                   - Verify model definitions and decorators
                   - Ensure proper imports and namespaces
                   - Check for TypeSpec-specific naming conventions

                   For C# Compiler Errors:
                   - Check for missing using statements
                   - Verify syntax correctness
                   - Look for missing references or assemblies
                   - Check for type mismatches or accessibility issues

                   For MSBuild Errors:
                   - Check project file syntax and references
                   - Verify package versions and compatibility
                   - Look for missing dependencies or targets
                   - Check for path or file access issues

                   For Analyzer Errors:
                   - Follow the specific rule guidelines mentioned in the error
                   - Check for code style or best practice violations
                   - Look for Azure SDK guideline compliance issues
                   - Verify naming conventions and API design patterns

                4. IMPLEMENTATION STEPS:
                   - Provide clear, step-by-step instructions
                   - Include specific code examples when possible
                   - Reference relevant documentation or guidelines
                   - Suggest verification steps to confirm the fix

                5. ADDITIONAL CONSIDERATIONS:
                   - If the error is unclear, suggest how to get more information
                   - Recommend best practices to prevent similar errors
                   - Consider impact on related code or dependencies
                   - Suggest testing approaches to validate the fix

                RESPONSE FORMAT:
                Start with identifying the likely error source, then provide a comprehensive solution with clear steps and examples.
                """,
                context: "FALLBACK RULE: Unknown error requiring general analysis\nORIGINAL ERROR: {0}\nBACKGROUND: This error doesn't match any specific rule patterns and requires general analysis to determine the appropriate fix. The error could originate from TypeSpec compilation, .NET compilation, MSBuild processes, or custom analyzers. The solution approach should adapt based on the error code pattern and message content to provide the most relevant guidance."
            );

            // TODO: Add other general rule prompts here when they are defined
            // Example:
            // builder["CS102"] = new AgentPromptFix("General rule prompt", "General rule context");
        }
    }
}
