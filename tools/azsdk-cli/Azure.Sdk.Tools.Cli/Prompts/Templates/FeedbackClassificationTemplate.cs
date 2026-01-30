// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for classifying SDK feedback and routing them to the appropriate phase.
/// Analyzes build failures, API review feedback, or user prompts to determine if TypeSpec 
/// customizations can help, if the task is complete, or if manual guidance is needed.
/// </summary>
public class FeedbackClassificationTemplate : BasePromptTemplate
{
    public override string TemplateId => "feedback-classification";
    public override string Version => "1.0.0";
    public override string Description => "Classify SDK feedback and route to appropriate phase";

    private readonly string? _serviceName;
    private readonly string? _language;
    private readonly string _request;
    private readonly string? _packagePath;
    private readonly string? _codeCustomizationDocUrl;

    /// <summary>
    /// Initializes a new classification template with the specified parameters.
    /// </summary>
    /// <param name="serviceName">The name of the service being customized (optional)</param>
    /// <param name="language">Target SDK language (e.g., python, csharp, java) (optional)</param>
    /// <param name="request">The full request context including history</param>
    /// <param name="packagePath">The absolute path to the SDK package directory (optional)</param>
    /// <param name="codeCustomizationDocUrl">URL to code customization documentation (optional)</param>
    /// <param name="iteration">Current iteration number (1-based)</param>
    /// <param name="isStalled">Whether the same error appeared twice consecutively</param>
    public FeedbackClassificationTemplate(
        string? serviceName,
        string? language,
        string request,
        string? packagePath = null,
        string? codeCustomizationDocUrl = null)
    {
        _serviceName = serviceName;
        _language = language;
        _request = request;
        _packagePath = packagePath;
        _codeCustomizationDocUrl = codeCustomizationDocUrl;
    }

    /// <summary>
    /// Builds the complete classification prompt.
    /// </summary>
    public override string BuildPrompt()
    {
        var taskInstructions = BuildTaskInstructions();
        var classificationConditions = BuildClassificationConditions();
        var examples = BuildExamples();
        var outputRequirements = BuildOutputRequirements();
        
        return BuildStructuredPrompt(taskInstructions, classificationConditions, examples, outputRequirements);
    }

    protected override string BuildSystemRole()
    {
        return $"""
        ## SYSTEM ROLE
        You are a classifier for the SDK customization workflow analyzing feedback items (build errors, API review comments, user requests).
        Your task: {Description} and determine the appropriate action path.
        
        ## SAFETY GUIDELINES
        - Follow Azure SDK standards and Microsoft policies
        - Do not process or expose sensitive information (credentials, secrets, personal data)
        - Refuse requests involving sensitive data - ask for clarification if uncertain
        - Provide accurate, actionable classifications based on TypeSpec capabilities
        """;
    }

    private string BuildTaskInstructions()
    {
        return $"""
        **Current Context:**
        - Service: {_serviceName ?? "N/A"}
        - Language: {_language ?? "N/A"}
        - Package Path: {_packagePath ?? "N/A"}
        - Code Customization Guide: {_codeCustomizationDocUrl ?? "N/A"}

        **Feedback to Classify:**
        {_request}

        **Task:**
        Analyze the feedback above and determine the appropriate classification: **PHASE_A**, **SUCCESS**, or **FAILURE**.

        {BuildClassificationConditions()}

        **Reference Documentation:**
        - TypeSpec Client Customizations: https://github.com/Azure/azure-rest-api-specs/blob/main/eng/common/knowledge/customizing-client-tsp.md
        {BuildCodeCustomizationDocSection()}

        **Available Tools:**
        - `read_file`: Read contents of specific files in the package directory
        - `list_dir`: List files and directories in the package path
        - `grep_search`: Search for patterns within files (use to find error locations, symbol definitions)
        - `fetch_webpage`: Fetch and read documentation from URLs
        """;
    }

    private string BuildCodeCustomizationDocSection()
    {
        if (string.IsNullOrEmpty(_codeCustomizationDocUrl))
        {
            return string.Empty;
        }

        return $"\n        - Language-specific customization guide: {_codeCustomizationDocUrl} (use fetch_webpage to access)";
    }



    private string BuildClassificationConditions()
    {
        return """
        **Decision Logic:**

        **If Context is NON-EMPTY** (check first):
        - Contains error indicators ("Failed", "error", "COMPILATION ERROR", "cannot find", "did not address") → **FAILURE**
          - Use grep_search to locate error sites and relevant code
          - Use read_file to examine specific files in the package
          - Use fetch_webpage to read documentation for guidance
          - Provide concrete step-by-step guidance with actual file paths and line numbers
        - Contains success ("Successfully applied", "Build succeeded") → **SUCCESS**
        - Otherwise (unclear or no clear indicator) → **FAILURE**
          - Assume something went wrong if context exists but no success indicator

        **If Context is EMPTY** (first attempt):
        - Actionable (directive, error, request) → **PHASE_A**
        - Non-actionable (informational, "keep as is", past tense, build success) → **SUCCESS**

        **What counts as "Non-actionable" (SUCCESS when context empty):**
        - Explicit acceptance: "Keep as is", "No changes needed", "This is fine"
        - Past tense (already done): "Method was made private", "Client was renamed"
        - Informational: Explanations, questions, acknowledgments
        - Build success with no errors

        """;
    }

    private string BuildExamples()
    {
        return """
        **Example 1: Empty Context - Actionable**
        Text: "Rename FooClient to BarClient for .NET"
        Context: ""
        
        Classification: PHASE_A
        Reason: Empty context, actionable request
        Next Action: Apply @@clientName(FooClient, "BarClient", "csharp") to client.tsp

        ---

        **Example 2: Non-Empty Context - TypeSpec Failed**
        Text: "Rename FooClient to BarClient for .NET"
        Context: "TypeSpec Customizations Failed: client.tsp not found"
        
        Classification: FAILURE
        Reason: Context shows TypeSpec failure
        Next Action: [Use read_file/list_dir to inspect structure. Provide steps to create partial class.]

        ---

        **Example 3: Non-Empty Context - Build Error**
        Text: "Build log"
        Context: "COMPILATION ERROR: cannot find symbol urlSource"
        
        Classification: FAILURE
        Reason: Context contains compilation error
        Next Action: [Use tools to inspect files. Fetch docs. Provide fix with actual paths.]

        ---

        **Example 4: Non-Empty Context - Success**
        Text: "Rename FooClient to BarClient"
        Context: "Added @@clientName(...). Build succeeded."
        
        Classification: SUCCESS
        Reason: Context shows successful customization and build
        Next Action: Return context for review

        ---

        **Example 5: Empty Context - Non-Actionable**
        Text: "Keep this as is since both spellings are valid."
        Context: ""
        
        Classification: SUCCESS
        Reason: Explicitly states to keep as is
        Next Action: Mark as resolved

        """;
    }

    private string BuildOutputRequirements()
    {
        return """
        **Required Output Format:**
        ```
        Classification: [PHASE_A | SUCCESS | FAILURE]
        Reason: <one-line explanation of why this classification was chosen>
        Next Action: <what should happen next - be specific and actionable>
        ```

        **Response Guidelines:**
        - Classification must be exactly one of: PHASE_A, SUCCESS, or FAILURE
        - Reason must clearly state which condition triggered the classification
        - Next Action must be concrete and actionable with step-by-step instructions
        - For FAILURE: Use grep_search, read_file, and fetch_webpage to provide specific file paths, line numbers, and code changes needed
        - Use proper formatting and structure
        - Ensure response follows Azure SDK guidelines
        """;
    }

}
