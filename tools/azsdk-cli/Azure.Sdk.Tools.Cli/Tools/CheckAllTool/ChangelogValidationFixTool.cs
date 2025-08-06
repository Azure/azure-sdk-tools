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
    /// This MCP-only tool fixes changelog format violations found in SDK packages.
    /// </summary>
    [Description("Fix changelog format violations in SDK packages")]
    [McpServerToolType]
    public class ChangelogValidationFixTool : MCPTool
    {
        private readonly ILogger<ChangelogValidationFixTool> logger;
        private readonly IOutputService output;
        private readonly Option<string> packagePathOption = new(["--package-path", "-p"], "Path to the package directory to check") { IsRequired = true };

        public ChangelogValidationFixTool(ILogger<ChangelogValidationFixTool> logger, IOutputService output) : base()
        {
            this.logger = logger;
            this.output = output;
        }

        public override Command GetCommand()
        {
            Command command = new("changelogValidationFix", "Return changelog validation fix prompt for SDK packages");
            command.AddOption(packagePathOption);
            command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
            return command;
        }

        public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                var packagePath = ctx.ParseResult.GetValueForOption(packagePathOption);
                var result = await FixChangelogValidation(packagePath);

                output.Output(result);
                ctx.ExitCode = ExitCode;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while running changelog validation fix");
                SetFailure(1);
                output.Output(new DefaultCommandResponse
                {
                    ResponseError = $"Error occurred while running changelog validation fix: {ex.Message}"
                });
                ctx.ExitCode = ExitCode;
            }
        }

        [McpServerTool(Name = "FixChangelogValidation"), Description("Fix changelog format violations in SDK packages. Provide absolute path to package root as param.")]
        public async Task<ICLICheckResponse> FixChangelogValidation(string packagePath)
        {
            try
            {
                logger.LogInformation($"Generating changelog validation fix prompt for package at: {packagePath}");
                
                if (!Directory.Exists(packagePath))
                {
                    SetFailure(1);
                    return new FailureCLICheckResponse(1, "", $"Package path does not exist: {packagePath}");
                }

                // Find CHANGELOG.md file
                var changelogPath = Path.Combine(packagePath, "CHANGELOG.md");
                if (!File.Exists(changelogPath))
                {
                    // Try alternative locations
                    changelogPath = Directory.GetFiles(packagePath, "CHANGELOG.md", SearchOption.AllDirectories).FirstOrDefault();
                    
                    if (string.IsNullOrEmpty(changelogPath))
                    {
                        return new FailureCLICheckResponse(1, "", $"No CHANGELOG.md file found in package at: {packagePath}");
                    }
                }

                var changelogContent = await File.ReadAllTextAsync(changelogPath);
                var prompt = GenerateChangelogFixPrompt(changelogContent, packagePath);

                return new SuccessCLICheckResponse(0, System.Text.Json.JsonSerializer.Serialize(new
                {
                    Message = "Changelog validation fix prompt generated successfully.",
                    Prompt = prompt,
                    ChangelogPath = changelogPath,
                    PackagePath = packagePath,
                    Instructions = "Use this prompt with an LLM to fix changelog format violations. The LLM should return the corrected changelog content."
                }));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while generating changelog validation fix prompt");
                SetFailure(1);
                return new FailureCLICheckResponse(1, "", $"Unhandled exception: {ex.Message}");
            }
        }

        private string GenerateChangelogFixPrompt(string changelogContent, string packagePath)
        {
            var packageName = Path.GetFileName(packagePath);
            
            return $@"# Changelog Validation Fix Task

You are an expert at fixing Azure SDK changelog format violations. Please analyze and fix the following changelog content according to Azure SDK standards.

## Package Context
- **Package Path**: {packagePath}
- **Package Name**: {packageName}

## Azure SDK Changelog Standards
1. **Header Format**: `# Release History`
2. **Version Format**: `## [Version] - [Date]` or `## [Version] (Unreleased)`
3. **Breaking Changes**: Must be listed first under each version
4. **Change Categories**: Features and Enhancements, Key Bug Fixes, Other Changes
5. **Entry Format**: Each change should be a bullet point with clear description
6. **Consistent Dating**: Use YYYY-MM-DD format for release dates
7. **Unreleased Section**: Should be at the top for upcoming changes

## Current Changelog Content
```markdown
{changelogContent}
```

## Instructions
Please fix any format violations in the changelog above and return the corrected markdown content. Ensure:
- Proper header format
- Consistent version and date formatting
- Breaking changes listed first when present
- Appropriate categorization of changes
- Clear, concise change descriptions
- Chronological ordering (newest first)

Return only the corrected changelog markdown content.";
        }
    }
}