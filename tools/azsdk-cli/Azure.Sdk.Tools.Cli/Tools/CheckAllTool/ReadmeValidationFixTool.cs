// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using Azure.Sdk.Tools.Cli.Models;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools.CheckAllTool
{
    /// <summary>
    /// This MCP-only tool fixes README standard violations found in SDK projects.
    /// </summary>
    [Description("Fix README standard violations in SDK projects")]
    [McpServerToolType]
    public class ReadmeValidationFixTool : MCPTool
    {
        private readonly ILogger<ReadmeValidationFixTool> logger;

        public ReadmeValidationFixTool(ILogger<ReadmeValidationFixTool> logger) : base()
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

        [McpServerTool(Name = "FixReadmeValidation"), Description("Fix README standard violations in SDK projects. Provide absolute path to project root as param.")]
        public async Task<DefaultCommandResponse> FixReadmeValidation(string projectPath)
        {
            try
            {
                logger.LogInformation($"Generating README validation fix prompt for project at: {projectPath}");
                
                if (!Directory.Exists(projectPath))
                {
                    SetFailure(1);
                    return new DefaultCommandResponse
                    {
                        ResponseError = $"Project path does not exist: {projectPath}"
                    };
                }

                // Find README.md file
                var readmePath = Path.Combine(projectPath, "README.md");
                if (!File.Exists(readmePath))
                {
                    // Try alternative locations
                    readmePath = Directory.GetFiles(projectPath, "README.md", SearchOption.AllDirectories).FirstOrDefault();
                    
                    if (string.IsNullOrEmpty(readmePath))
                    {
                        return new DefaultCommandResponse
                        {
                            ResponseError = $"No README.md file found in project at: {projectPath}"
                        };
                    }
                }

                var readmeContent = await File.ReadAllTextAsync(readmePath);
                var prompt = GenerateReadmeFixPrompt(readmeContent, projectPath);

                return new DefaultCommandResponse
                {
                    Message = "README validation fix prompt generated successfully.",
                    Result = new
                    {
                        Prompt = prompt,
                        ReadmePath = readmePath,
                        ProjectPath = projectPath,
                        Instructions = "Use this prompt with an LLM to fix README standard violations. The LLM should return the corrected README content."
                    }
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception while generating README validation fix prompt");
                SetFailure(1);
                return new DefaultCommandResponse
                {
                    ResponseError = $"Unhandled exception: {ex.Message}"
                };
            }
        }

        private string GenerateReadmeFixPrompt(string readmeContent, string projectPath)
        {
            var projectName = Path.GetFileName(projectPath);
            
            return $@"# README Validation Fix Task

You are an expert at fixing Azure SDK README standard violations. Please analyze and fix the following README content according to Azure SDK standards.

## Project Context
- **Project Path**: {projectPath}
- **Project Name**: {projectName}

## Azure SDK README Standards
1. **Header Format**: Use clear project title with Azure branding
2. **Installation Section**: Include pip install instructions for Python packages
3. **Quick Start Section**: Provide basic usage examples with authentication
4. **Key Concepts Section**: Explain main concepts and terminology
5. **Examples Section**: Include comprehensive code examples
6. **Optional Configuration Section**: Document configuration options if applicable
7. **Troubleshooting Section**: Common issues and solutions
8. **Next Steps Section**: Links to additional resources
9. **Contributing Section**: Link to CONTRIBUTING.md
10. **Code of Conduct**: Link to Microsoft Code of Conduct
11. **License**: MIT License reference
12. **Proper Code Blocks**: Use correct language tags for syntax highlighting
13. **Consistent Formatting**: Ensure consistent spacing, headers, and structure
14. **Working Links**: All links should be valid and accessible
15. **Badge Standards**: Include appropriate build status and package version badges

## Required Sections (in order)
1. # [Package Name]
2. ## Getting started
3. ### Install the package
4. ### Prerequisites
5. ### Authenticate the client
6. ## Key concepts
7. ## Examples
8. ## Optional Configuration
9. ## Troubleshooting
10. ## Next steps
11. ## Contributing
12. ## Code of Conduct
13. ## License

## Instructions
1. Analyze the current README for standard violations
2. Fix all identified issues while preserving the original content meaning
3. Ensure compliance with Azure SDK README standards
4. Add missing sections if needed
5. Standardize formatting and structure
6. Fix broken links and improve code examples
7. Return ONLY the corrected README content in markdown format

## Current README Content
```markdown
{readmeContent}
```

## Expected Output
Return the corrected README content in markdown format, ensuring it follows all Azure SDK standards. Do not include any explanations or additional text - just the corrected README.";
        }
    }
}