// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Azure.Sdk.Tools.Cli.Prompts;

/// <summary>
/// Base abstract class for prompt templates providing common safety guidelines and structure.
/// Templates implement BuildPrompt with their specific parameters for type safety and simplicity.
/// </summary>
public abstract class BasePromptTemplate
{
    public abstract string TemplateId { get; }
    public abstract string Version { get; }
    public abstract string Description { get; }

    /// <summary>
    /// Builds the complete prompt using the configured parameters.
    /// This method must be implemented by derived classes to provide their specific prompt building logic.
    /// </summary>
    /// <returns>Complete structured prompt</returns>
    public abstract string BuildPrompt();

    /// <summary>
    /// Builds a complete prompt with safety guidelines and structured sections.
    /// </summary>
    protected string BuildStructuredPrompt(string taskInstructions, string? constraints = null, string? examples = null, string? outputRequirements = null)
    {
        var prompt = new StringBuilder();

        // 1. System Role & Guidelines
        prompt.AppendLine(BuildSystemRole());
        prompt.AppendLine();

        // 2. Task Instructions
        prompt.AppendLine("## TASK INSTRUCTIONS");
        prompt.AppendLine(taskInstructions);
        prompt.AppendLine();

        // 3. Constraints (if any)
        if (!string.IsNullOrEmpty(constraints))
        {
            prompt.AppendLine("## CONSTRAINTS");
            prompt.AppendLine(constraints);
            prompt.AppendLine();
        }

        // 4. Examples (if any)
        if (!string.IsNullOrEmpty(examples))
        {
            prompt.AppendLine("## EXAMPLES");
            prompt.AppendLine(examples);
            prompt.AppendLine();
        }

        // 5. Output Requirements
        prompt.AppendLine("## OUTPUT REQUIREMENTS");
        prompt.AppendLine(outputRequirements ?? GetDefaultOutputRequirements());

        return prompt.ToString();
    }

    /// <summary>
    /// Builds the system role section of the prompt.
    /// </summary>
    protected virtual string BuildSystemRole()
    {
        return $"""
        ## SYSTEM ROLE
        You are an AI assistant for Azure SDK development. Your task: {Description.ToLower()}.
        
        ## SAFETY GUIDELINES
        - Follow Azure SDK standards and Microsoft policies
        - Do not process or expose sensitive information (credentials, secrets, personal data)
        - Refuse requests involving sensitive data - ask for clarification if uncertain
        - Provide accurate, helpful responses that follow security best practices
        """;
    }

    /// <summary>
    /// Gets the default output requirements.
    /// </summary>
    protected virtual string GetDefaultOutputRequirements()
    {
        return """
        - Provide clear, actionable responses
        - Use proper formatting and structure
        - Ensure output follows Azure SDK guidelines
        """;
    }
}
