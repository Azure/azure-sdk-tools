// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.CheckAllTool
{
    /// <summary>
    /// This MCP-only tool fixes changelog format violations found in SDK projects.
    /// </summary>
    [Description("Fix changelog format violations in SDK projects")]
    [McpServerToolType]
    public class ChangelogValidationFixTool : MCPTool
    {
        private readonly ILogger<ChangelogValidationFixTool> logger;

        public ChangelogValidationFixTool(ILogger<ChangelogValidationFixTool> logger) : base()
        {
            this.logger = logger;
        }

        public override Command GetCommand()
        {
            // MCP-only tool - no CLI command
            return null!;
        }

        public override Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            // MCP-only tool - no CLI command handling
            throw new NotImplementedException("This tool is available only through MCP server interface");
        }

        [McpServerTool(Name = "FixChangelogValidation"), Description("Fix changelog format violations in SDK projects. Provide absolute path to project root as param.")]
        public async Task<IOperationResult> FixChangelogValidation(string projectPath)
        {
            try
            {
                logger.LogInformation($"Generating changelog validation fix prompt for project at: {projectPath}");
                
                if (!Directory.Exists(projectPath))
                {
                    SetFailure(1);
                    return new FailureResult(1, "", $"Project path does not exist: {projectPath}");
                }

                // Find CHANGELOG.md file
                var changelogPath = Path.Combine(projectPath, "CHANGELOG.md");
                if (!File.Exists(changelogPath))
                {
                    // Try alternative locations
                    changelogPath = Directory.GetFiles(projectPath, "CHANGELOG.md", SearchOption.AllDirectories).FirstOrDefault();
                    
                    if (string.IsNullOrEmpty(changelogPath))
                    {
                        return new FailureResult(1, "", $"No CHANGELOG.md file found in project at: {projectPath}");
                    }
                }

                var changelogContent = await File.ReadAllTextAsync(changelogPath);
                var prompt = GenerateChangelogFixPrompt(changelogContent, projectPath);

                return new SuccessResult(0, System.Text.Json.JsonSerializer.Serialize(new
                {
                    Message = "Changelog validation fix prompt generated successfully.",
                    Prompt = prompt,
                    ChangelogPath = changelogPath,
                    ProjectPath = projectPath,
                    Instructions = "Use this prompt with an LLM to fix changelog format violations. The LLM should return the corrected changelog content."
                }));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while generating changelog validation fix prompt");
                SetFailure(1);
                return new FailureResult(1, "", $"Unhandled exception: {ex.Message}");
            }
        }

        private string GenerateChangelogFixPrompt(string changelogContent, string projectPath)
        {
            var projectName = Path.GetFileName(projectPath);
            
            return $@"# Changelog Validation Fix Task

You are an expert at fixing Azure SDK changelog format violations. Please analyze and fix the following changelog content according to Azure SDK standards.

## Project Context
- **Project Path**: {projectPath}
- **Project Name**: {projectName}

## Azure SDK Changelog Standards
1. **Header Format**: Use `# Release History` as the main header
2. **Version Format**: Use `## 1.0.0 (2023-01-01)` format with proper date
3. **Unreleased Section**: Always include `## 1.0.0-beta.1 (Unreleased)` at the top for upcoming changes
4. **Category Headers**: Use standard categories:
   - ### Features Added
   - ### Breaking Changes
   - ### Bugs Fixed
   - ### Other Changes
5. **Date Format**: Use ISO format YYYY-MM-DD in parentheses
6. **Chronological Order**: Most recent version first
7. **Proper Bullet Points**: Use `-` for list items with proper indentation
8. **No Empty Sections**: Remove empty category sections
9. **Consistent Formatting**: Ensure consistent spacing and formatting throughout

## Instructions
1. Analyze the current changelog for format violations
2. Fix all identified issues while preserving the original content meaning
3. Ensure compliance with Azure SDK changelog standards
4. Add missing sections if needed (like Unreleased section)
5. Standardize date formats and version headers
6. Organize content in proper chronological order
7. Return ONLY the corrected changelog content in markdown format

## Expected Output
Return the corrected changelog content in markdown format, ensuring it follows all Azure SDK standards. Do not include any explanations or additional text - just the corrected changelog.";
        }
    }
}