// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for providing manual update guidance when TypeSpec decorators cannot address feedback.
/// This is the second stage prompt, invoked only for FAILURE-classified items, to provide
/// specific code customization guidance based on the SDK package files (if available) or
/// general guidance with a link to the code customization documentation.
/// </summary>
public class ManualUpdateGuidanceTemplate : BasePromptTemplate
{
    public override string TemplateId => "manual-update-guidance";
    public override string Version => "1.0.0";
    public override string Description => "Provide manual code customization guidance for feedback that TypeSpec decorators cannot address";

    private readonly string _feedbackText;
    private readonly string _reason;
    private readonly string? _language;
    private readonly string? _codeCustomizationDocUrl;
    private readonly string? _packagePath;

    /// <summary>
    /// Initializes a new manual update guidance template.
    /// </summary>
    /// <param name="feedbackText">The original feedback text</param>
    /// <param name="reason">The classification reason explaining why TypeSpec decorators cannot address this</param>
    /// <param name="language">Target SDK language (e.g., python, csharp, java) (optional)</param>
    /// <param name="codeCustomizationDocUrl">URL to language-specific code customization documentation (optional)</param>
    /// <param name="packagePath">Path to the SDK package directory for file inspection (optional)</param>
    public ManualUpdateGuidanceTemplate(
        string feedbackText,
        string reason,
        string? language = null,
        string? codeCustomizationDocUrl = null,
        string? packagePath = null)
    {
        _feedbackText = feedbackText;
        _reason = reason;
        _language = language;
        _codeCustomizationDocUrl = codeCustomizationDocUrl;
        _packagePath = packagePath;
    }

    public override string BuildPrompt()
    {
        var taskInstructions = BuildTaskInstructions();
        var constraints = BuildConstraints();
        var outputRequirements = BuildOutputRequirements();

        return BuildStructuredPrompt(taskInstructions, constraints, examples: null, outputRequirements);
    }

    protected override string BuildSystemRole()
    {
        return $"""
        ## SYSTEM ROLE
        You are a code customization advisor for Azure SDKs. A previous classification determined that 
        TypeSpec decorators cannot address certain feedback, and your job is to provide specific, 
        actionable guidance on how to make the necessary code-level changes.
        
        ## SAFETY GUIDELINES
        - Follow Azure SDK standards and Microsoft policies
        - Do not process or expose sensitive information (credentials, secrets, personal data)
        - Refuse requests involving sensitive data - ask for clarification if uncertain
        - Provide accurate guidance based on the SDK's existing code structure
        """;
    }

    private string BuildTaskInstructions()
    {
        var instructions = $"""
        **Feedback that needs code customization:**
        {_feedbackText}

        **Why TypeSpec decorators cannot address this:**
        {_reason}

        **Language:** {_language ?? "N/A"}
        """;

        if (!string.IsNullOrEmpty(_packagePath))
        {
            instructions += $"""

            **SDK Package Path:** {_packagePath}

            **Task:**
            Inspect the SDK package files to understand the current code structure and provide specific 
            guidance on which files to modify and what changes to make. Use the available tools to:
            1. List files in the package to understand the project structure
            2. Read relevant source files to understand existing patterns
            3. Search for the symbols or patterns mentioned in the feedback
            
            Then provide concrete, actionable guidance with specific file paths and suggested changes.

            **Available Tools:**
            - `read_file`: Read contents of specific files in the SDK package
            - `list_dir`: List files and directories
            - `grep_search`: Search for patterns within files
            """;
        }
        else
        {
            instructions += $"""

            **Task:**
            Provide general guidance on how to address this feedback through code customizations.
            Since no SDK package path is available, provide guidance based on common SDK patterns
            and the code customization documentation.
            """;
        }

        if (!string.IsNullOrEmpty(_codeCustomizationDocUrl))
        {
            instructions += $"""

            **Code Customization Documentation:** {_codeCustomizationDocUrl}
            Reference this documentation URL in your guidance for detailed instructions.
            """;
        }

        return instructions;
    }

    private string BuildConstraints()
    {
        return """
        - Focus on practical, minimal changes needed to address the feedback
        - Reference specific files and code patterns when package files are available
        - Do not suggest TypeSpec decorator changes (those have already been ruled out)
        - Keep guidance concise and actionable
        - If the feedback is ambiguous, state what assumptions you are making
        """;
    }

    private string BuildOutputRequirements()
    {
        return """
        Provide your guidance as freeform text. Structure it clearly with:
        - A brief summary of what needs to change
        - Specific files to modify (if package path was inspected)
        - The changes to make in each file
        - Any relevant documentation links

        Do NOT wrap your response in Classification/Reason/Next Action format.
        Just provide the guidance directly as plain text.
        """;
    }
}
