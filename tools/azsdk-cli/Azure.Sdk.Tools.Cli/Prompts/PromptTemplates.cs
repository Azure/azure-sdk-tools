// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Prompts.Templates;

namespace Azure.Sdk.Tools.Cli.Prompts;

/// <summary>
/// Centralized prompt methods using the standardized template system.
/// All prompts include built-in safety guidelines and consistent structure.
/// </summary>
public static class PromptTemplates
{
    /// <summary>
    /// Gets a spelling fix prompt with built-in safety measures.
    /// </summary>
    /// <param name="cspellOutput">The output from cspell lint command</param>
    /// <param name="repositoryContext">Optional context about the repository being processed</param>
    /// <param name="additionalRules">Optional additional rules for spelling validation</param>
    /// <returns>Formatted prompt ready for consumption</returns>
    public static string GetSpellingFixPrompt(string cspellOutput, string repositoryContext = "Azure SDK repository", string? additionalRules = null)
    {
        var template = new SpellingValidationTemplate();
        return template.BuildPrompt(cspellOutput, repositoryContext, additionalRules);
    }

    /// <summary>
    /// Gets a README generation prompt using the standardized template system.
    /// </summary>
    /// <param name="templateContent">The README template content to fill in</param>
    /// <param name="serviceDocumentation">URL containing service documentation</param>
    /// <param name="packagePath">Package path for generating documentation links</param>
    /// <param name="additionalRules">Optional additional rules or constraints</param>
    /// <returns>Formatted prompt for README generation with built-in safety measures</returns>
    public static string GetReadMeGenerationPrompt(string templateContent, string serviceDocumentation, string packagePath, string? additionalRules = null)
    {
        var template = new ReadMeGenerationTemplate();
        return template.BuildPrompt(templateContent, serviceDocumentation, packagePath, additionalRules);
    }
}
