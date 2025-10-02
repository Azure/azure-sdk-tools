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
    /// Gets the microagent spelling fix prompt with built-in safety measures.
    /// </summary>
    /// <param name="cspellOutput">The output from cspell lint command</param>
    /// <param name="repositoryContext">Optional context about the repository being processed</param>
    /// <returns>Formatted prompt ready for microagent consumption</returns>
    public static string GetMicroagentSpellingFixPrompt(string cspellOutput, string repositoryContext = "Azure SDK repository")
    {
        return PromptTemplateHelper.BuildPrompt("spelling-validation", builder =>
            builder.WithParameter("cspell_output", cspellOutput)
                   .WithParameter("repository_context", repositoryContext));
    }

    /// <summary>
    /// Gets a README generation prompt using the standardized template system.
    /// </summary>
    /// <param name="templateContent">The README template content to fill in</param>
    /// <param name="serviceDocumentation">URL containing service documentation</param>
    /// <param name="packagePath">Package path for generating documentation links</param>
    /// <param name="additionalRules">Optional additional rules or constraints</param>
    /// <returns>Formatted prompt for README generation with built-in safety measures</returns>
    public static string GetReadMeGenerationPrompt(string templateContent, string serviceDocumentation, string packagePath, string additionalRules = "")
    {
        return PromptTemplateHelper.BuildPrompt("readme-generation", builder =>
            builder.WithParameter("template_content", templateContent)
                   .WithParameter("service_documentation", serviceDocumentation)
                   .WithParameter("package_path", packagePath)
                   .WithParameter("additional_rules", additionalRules));
    }

    /// <summary>
    /// Gets a log analysis prompt for analyzing build failures and providing structured diagnostics.
    /// </summary>
    /// <param name="logContent">The log content to analyze</param>
    /// <param name="logType">Type of log (default: Azure Pipelines build)</param>
    /// <param name="outputFormat">Output format (default: json)</param>
    /// <returns>Formatted prompt for log analysis with built-in safety measures</returns>
    public static string GetLogAnalysisPrompt(string logContent, string logType = "Azure Pipelines build", string outputFormat = "json")
    {
        return PromptTemplateHelper.BuildPrompt("log-analysis", builder =>
            builder.WithParameter("log_content", logContent)
                   .WithParameter("log_type", logType)
                   .WithParameter("output_format", outputFormat));
    }

    /// <summary>
    /// Registers all built-in prompt templates.
    /// Call this method once during application startup.
    /// </summary>
    public static void RegisterBuiltInTemplates()
    {
        var registry = PromptTemplateRegistry.Instance;
        
        // Register all built-in templates (idempotent)
        if (!registry.TryGetTemplate("spelling-validation", out _)) 
        {
            registry.RegisterTemplate(new SpellingValidationTemplate());
        }
            
        if (!registry.TryGetTemplate("readme-generation", out _))
        {
           registry.RegisterTemplate(new ReadMeGenerationTemplate());
        }

        if (!registry.TryGetTemplate("log-analysis", out _))
        {
            registry.RegisterTemplate(new LogAnalysisTemplate());
        }
    }
}
