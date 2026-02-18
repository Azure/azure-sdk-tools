// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for providing manual update guidance for multiple FAILURE items in a single batch.
/// This reduces session overhead by processing all failure items in one LLM call.
/// </summary>
public class BatchManualUpdateGuidanceTemplate : BasePromptTemplate
{
    public override string TemplateId => "batch-manual-update-guidance";
    public override string Version => "1.0.0";
    public override string Description => "Provide manual code customization guidance for multiple feedback items in batch";

    private readonly List<FeedbackItem> _items;
    private readonly string? _language;
    private readonly string? _codeCustomizationDocUrl;
    private readonly string? _packagePath;

    /// <summary>
    /// Initializes a new batch manual update guidance template.
    /// </summary>
    /// <param name="items">The feedback items that need manual guidance</param>
    /// <param name="language">Target SDK language (e.g., python, csharp, java) (optional)</param>
    /// <param name="codeCustomizationDocUrl">URL to language-specific code customization documentation (optional)</param>
    /// <param name="packagePath">Path to the SDK package directory for file inspection (optional)</param>
    public BatchManualUpdateGuidanceTemplate(
        List<FeedbackItem> items,
        string? language = null,
        string? codeCustomizationDocUrl = null,
        string? packagePath = null)
    {
        _items = items;
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
        var languageLabel = _language ?? "the target language";
        return $"""
        ## SYSTEM ROLE
        You are an Azure SDK for {languageLabel} expert. A previous classification determined that 
        TypeSpec decorators cannot address certain feedback items, and your job is to provide specific, 
        actionable guidance on how to make the necessary code-level changes in the SDK for EACH item.
        
        ## SAFETY GUIDELINES
        - Follow Azure SDK standards and Microsoft policies
        - Do not process or expose sensitive information (credentials, secrets, personal data)
        - Refuse requests involving sensitive data - ask for clarification if uncertain
        - Provide accurate guidance based on the SDK's existing code structure
        - Do NOT include any URLs or links unless they were explicitly provided to you in this prompt
        - Do NOT hallucinate documentation links or GitHub URLs
        """;
    }

    private string BuildTaskInstructions()
    {
        var itemsSection = string.Join("\n\n", _items.Select((item, index) => $"""
            --- ITEM {index + 1} ---
            ID: {item.Id}
            Feedback: {item.Text}
            Why TypeSpec cannot address: {item.ClassificationReason}
            """));

        var instructions = $"""
        **Language:** {_language ?? "N/A"}

        **Feedback Items Requiring Code Customization ({_items.Count} items):**

        {itemsSection}

        ---

        """;

        if (!string.IsNullOrEmpty(_packagePath))
        {
            instructions += $"""
            **SDK Package Path:** {_packagePath}

            **Task:**
            For EACH feedback item above, inspect the SDK package files to understand the current code 
            structure and provide specific guidance on which files to modify and what changes to make.
            
            Use the available tools to:
            1. List files in the package to understand the project structure
            2. Read relevant source files to understand existing patterns
            3. Search for the symbols or patterns mentioned in each feedback item
            
            Then provide concrete, actionable guidance with specific file paths and suggested changes
            for EACH item.

            **Available Tools:**
            - `read_file`: Read contents of specific files in the SDK package
            - `list_dir`: List files and directories
            - `grep_search`: Search for patterns within files
            """;
        }
        else
        {
            instructions += """
            **Task:**
            For EACH feedback item above, provide general guidance on how to address it through 
            code customizations. Since no SDK package path is available, provide guidance based 
            on common SDK patterns and the code customization documentation.
            """;
        }

        if (!string.IsNullOrEmpty(_codeCustomizationDocUrl))
        {
            instructions += $"""

            **Code Customization Documentation (AUTHORITATIVE):** {_codeCustomizationDocUrl}
            This is the official documentation for how to customize generated SDK code for this language.
            You MUST base your guidance on the patterns and approaches described in this documentation.
            Include this URL in your response for relevant items.
            """;
        }

        return instructions;
    }

    private string BuildConstraints()
    {
        return """
        - Provide guidance for ALL items - do not skip any
        - Focus on practical, minimal changes needed to address each feedback item
        - Reference specific files and code patterns when package files are available
        - Do not suggest TypeSpec decorator changes (those have already been ruled out)
        - Keep guidance concise and actionable
        - If feedback is ambiguous, state what assumptions you are making
        - Do NOT include any URLs or documentation links that were not explicitly provided in this prompt
        - Do NOT hallucinate or fabricate links to GitHub repositories, documentation pages, or any other resources
        - The ONLY URL you may include is the Code Customization Documentation URL provided above (if any)
        """;
    }

    private string BuildOutputRequirements()
    {
        return """
        **CRITICAL: Required Output Format**

        You MUST output one guidance block per feedback item, using the exact item ID in square brackets as a header.
        Every item MUST appear in the output. Do NOT skip any items.

        ```
        [<item-id>]
        <Your guidance here - include summary, files to modify, and specific changes>

        [<next-item-id>]
        <Your guidance here>
        ```

        **Rules:**
        - The `[<item-id>]` header MUST match the exact ID from each feedback item
        - Provide clear, actionable guidance for each item
        - Include specific file paths when SDK package was inspected
        - Include the Code Customization Documentation URL where relevant (if provided)
        - Output ALL items — every single item ID must appear in your response
        - Do NOT add any text before or after the guidance blocks
        - Do NOT include any URLs that were not explicitly provided in this prompt
        """;
    }
}
