// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for log analysis prompts, particularly for Azure Pipeline failures.
/// This template provides structured analysis of logs with JSON output requirements.
/// </summary>
public class LogAnalysisTemplate : BasePromptTemplate
{
    public override string TemplateId => "log-analysis";
    public override string Version => "1.0.0";
    public override string Description => "Analyze logs for failures, errors, and provide structured diagnostics";


    protected override IEnumerable<string> RequiredParameters => new[] { "log_content" };
    protected override IEnumerable<string> OptionalParameters => new[] 
    { 
        "log_type", 
        "focus_areas",
        "error_patterns",
        "output_format"
    };

    protected override string BuildTaskInstructions(IPromptContext context)
    {
        var logContent = context.GetParameter<string>("log_content") ?? "";
        var logType = context.GetParameter<string>("log_type") ?? "Azure Pipelines build";
        var focusAreas = context.GetParameter<string>("focus_areas") ?? "";

        return $"""
        You are an assistant that analyzes {logType} logs.
        
        You will be provided with log files and your task is to analyze them for failures and issues.
        
        **Analysis Requirements:**
        - Identify the root cause of failures, not just surface-level errors
        - Include relevant data like error type, error messages, functions, and error lines
        - Find other log lines that may be descriptive of the problem
        - Look beyond generic errors like 'PowerShell exited with code 1' to find the actual error message
        - Focus on actionable information that helps resolve the issue
        
        **Log Content to Analyze:**
        ```
        {logContent}
        ```
        """ + (!string.IsNullOrEmpty(focusAreas) ? $"\n\n**Specific Focus Areas:**\n{focusAreas}" : "");
    }

    protected override string BuildTaskConstraints(IPromptContext context)
    {
        var errorPatterns = context.GetParameter<string>("error_patterns") ?? "";

        return $"""
        **Analysis Guidelines:**
        - Look for error patterns in chronological order
        - Distinguish between symptoms and root causes
        - Identify cascading failures vs. primary failures
        - Extract relevant context around error messages
        - Ignore noise and focus on actionable errors
        - Consider environment-specific issues (permissions, network, dependencies)
        
        **Common Error Categories to Look For:**
        - Compilation/build errors
        - Test failures
        - Dependency resolution issues
        - Permission/access errors
        - Network/connectivity problems
        - Configuration errors
        - Resource availability issues
        """ + (!string.IsNullOrEmpty(errorPatterns) ? $"\n\n**Additional Error Patterns:**\n{errorPatterns}" : "");
    }

    protected override string BuildOutputRequirements(IPromptContext context)
    {
        var outputFormat = context.GetParameter<string>("output_format") ?? "json";

        if (outputFormat.ToLower() == "json")
        {
            return """
            **CRITICAL**: The full response must be parsed as valid JSON. Do not prefix with any other message.
            
            Provide suggested next steps and respond ONLY in valid JSON with a single object in the following format:
            
            ```json
            {
                "summary": "Brief description of the primary issue and impact",
                "root_cause": "The underlying cause of the failure",
                "errors": [
                    {
                        "file": "filename or component",
                        "line": 123,
                        "message": "detailed error description",
                        "severity": "error|warning|info",
                        "category": "build|test|dependency|permission|network|config"
                    }
                ],
                "suggested_fixes": [
                    {
                        "description": "What to do",
                        "priority": "high|medium|low",
                        "steps": [
                            "Step 1: Specific action",
                            "Step 2: Another action"
                        ]
                    }
                ],
                "related_logs": [
                    {
                        "line_number": 456,
                        "content": "relevant log line that provides context",
                        "relevance": "why this line is important"
                    }
                ]
            }
            ```
            
            **JSON Validation Requirements:**
            - Entire response must be valid JSON
            - All strings must be properly escaped
            - No trailing commas
            - All required fields must be present
            - Arrays can be empty but must exist
            """;
        }
        else
        {
            return """
            Provide a structured analysis including:
            - Executive summary of the issue
            - Root cause analysis
            - Detailed error breakdown
            - Recommended remediation steps
            - Prevention strategies
            
            Use clear headings and bullet points for readability.
            """;
        }
    }

    protected override string BuildExamples(IPromptContext context)
    {
        return """
        **Example Error Analysis:**
        
        **Surface Error:** "PowerShell exited with code 1"
        **Root Cause:** Authentication failure due to expired service principal
        **Relevant Context:** Lines showing "AADSTS70002: Error validating credentials"
        
        **Good Analysis:**
        ```json
        {
            "summary": "Build failed due to expired service principal credentials",
            "root_cause": "Service principal authentication expired, preventing access to Azure resources",
            "errors": [
                {
                    "file": "deploy.ps1",
                    "line": 45,
                    "message": "AADSTS70002: Error validating credentials",
                    "severity": "error",
                    "category": "permission"
                }
            ],
            "suggested_fixes": [
                {
                    "description": "Renew service principal credentials",
                    "priority": "high",
                    "steps": [
                        "Contact Azure admin to renew service principal",
                        "Update pipeline variables with new credentials",
                        "Test connection before next deployment"
                    ]
                }
            ]
        }
        ```
        
        **Poor Analysis:**
        ```json
        {
            "summary": "PowerShell script failed",
            "root_cause": "Script returned exit code 1",
            "errors": [
                {
                    "message": "Process exited with code 1"
                }
            ]
        }
        ```
        """;
    }
}
