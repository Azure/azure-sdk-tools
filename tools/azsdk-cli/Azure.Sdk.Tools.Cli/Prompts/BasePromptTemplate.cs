// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Azure.Sdk.Tools.Cli.Prompts;

/// <summary>
/// Base abstract class for prompt templates providing common safety guidelines and structure.
/// </summary>
public abstract class BasePromptTemplate : IPromptTemplate
{
    public abstract string TemplateId { get; }
    public abstract string Version { get; }
    public abstract string Description { get; }

    /// <summary>
    /// Gets the required parameters for this prompt template.
    /// </summary>
    protected abstract IEnumerable<string> RequiredParameters { get; }

    /// <summary>
    /// Gets the optional parameters for this prompt template.
    /// </summary>
    protected virtual IEnumerable<string> OptionalParameters => Enumerable.Empty<string>();

    /// <summary>
    /// Builds the main task instructions for the prompt.
    /// </summary>
    /// <param name="context">The prompt context</param>
    /// <returns>The task-specific instructions</returns>
    protected abstract string BuildTaskInstructions(IPromptContext context);

    /// <summary>
    /// Builds additional constraints specific to the task.
    /// </summary>
    /// <param name="context">The prompt context</param>
    /// <returns>Task-specific constraints</returns>
    protected virtual string BuildTaskConstraints(IPromptContext context) => string.Empty;

    /// <summary>
    /// Builds examples for the prompt if applicable.
    /// </summary>
    /// <param name="context">The prompt context</param>
    /// <returns>Examples section</returns>
    protected virtual string BuildExamples(IPromptContext context) => string.Empty;

    public virtual string BuildPrompt(IPromptContext context)
    {
        var validation = ValidateContext(context);
        if (!validation.IsValid)
        {
            throw new ArgumentException($"Invalid prompt context: {string.Join(", ", validation.Errors)}");
        }

        var prompt = new StringBuilder();

        // 1. System Role & Guidelines
        prompt.AppendLine(BuildSystemRole());
        prompt.AppendLine();

        // 2. Task Instructions
        prompt.AppendLine("## TASK INSTRUCTIONS");
        prompt.AppendLine(BuildTaskInstructions(context));
        prompt.AppendLine();

        // 3. Constraints (if any)
        var constraints = BuildTaskConstraints(context);
        if (!string.IsNullOrEmpty(constraints))
        {
            prompt.AppendLine("## CONSTRAINTS");
            prompt.AppendLine(constraints);
            prompt.AppendLine();
        }

        // 4. Examples (if any)
        var examples = BuildExamples(context);
        if (!string.IsNullOrEmpty(examples))
        {
            prompt.AppendLine("## EXAMPLES");
            prompt.AppendLine(examples);
            prompt.AppendLine();
        }

        // 5. Output Requirements
        prompt.AppendLine("## OUTPUT REQUIREMENTS");
        prompt.AppendLine(BuildOutputRequirements(context));

        return prompt.ToString();
    }

    public virtual PromptValidationResult ValidateContext(IPromptContext context)
    {
        var result = new PromptValidationResult();

        // Check required parameters
        foreach (var required in RequiredParameters)
        {
            if (!context.HasParameter(required) || context.GetParameter(required) == null)
            {
                result.AddError($"Required parameter '{required}' is missing");
            }
        }

        return result;
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
    /// Builds the output format requirements section.
    /// </summary>
    protected virtual string BuildOutputRequirements(IPromptContext context)
    {
        return """
        - Provide clear, actionable responses
        - Use proper formatting and structure
        - Ensure output follows Azure SDK guidelines
        """;
    }


}
