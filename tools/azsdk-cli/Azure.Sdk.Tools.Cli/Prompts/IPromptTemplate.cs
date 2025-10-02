// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts;

/// <summary>
/// Interface for prompt templates that provide structured, safe, and consistent prompts for AI models.
/// </summary>
public interface IPromptTemplate
{
    /// <summary>
    /// The unique identifier for this prompt template.
    /// </summary>
    string TemplateId { get; }

    /// <summary>
    /// The version of this prompt template for tracking changes and compatibility.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// A brief description of what this prompt template is used for.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Builds the complete prompt string with all safety guidelines, policies, and instructions.
    /// </summary>
    /// <param name="context">Context-specific parameters for the prompt</param>
    /// <returns>The complete, formatted prompt ready for AI model consumption</returns>
    string BuildPrompt(IPromptContext context);

    /// <summary>
    /// Validates that the prompt context contains all required parameters.
    /// </summary>
    /// <param name="context">The context to validate</param>
    /// <returns>Validation result with any errors or warnings</returns>
    PromptValidationResult ValidateContext(IPromptContext context);
}
