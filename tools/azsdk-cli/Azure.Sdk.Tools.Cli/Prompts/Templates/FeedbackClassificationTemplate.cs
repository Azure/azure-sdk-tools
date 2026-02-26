// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for classifying SDK feedback and routing them to the appropriate phase.
/// Supports batch classification of multiple feedback items in a single LLM call,
/// with strictly formatted ID-keyed output for robust parsing.
/// </summary>
public class FeedbackClassificationTemplate : BasePromptTemplate
{
    public override string TemplateId => "feedback-classification";
    public override string Version => "1.0.0";
    public override string Description => "Classify SDK feedback items in batch and route to appropriate phase";

    private readonly string? _serviceName;
    private readonly string? _language;
    private readonly string _referenceDocContent;
    private readonly List<FeedbackItem> _items;
    private readonly string _globalContext;

    /// <summary>
    /// Initializes a new batch classification template.
    /// </summary>
    /// <param name="serviceName">The name of the service being customized</param>
    /// <param name="language">Target SDK language (e.g., python, csharp, java) (optional)</param>
    /// <param name="referenceDocContent">Content of the customizing-client-tsp.md reference document</param>
    /// <param name="items">The feedback items to classify</param>
    /// <param name="globalContext">Global context containing all changes and history</param>
    public FeedbackClassificationTemplate(
        string? serviceName,
        string? language,
        string referenceDocContent,
        List<FeedbackItem> items,
        string globalContext)
    {
        _serviceName = serviceName;
        _language = language;
        _referenceDocContent = referenceDocContent;
        _items = items;
        _globalContext = globalContext;
    }

    public override string BuildPrompt()
    {
        var taskInstructions = BuildTaskInstructions();
        var constraints = BuildClassificationConditions();
        var examples = BuildExamples();
        var outputRequirements = BuildOutputRequirements();
        
        return BuildStructuredPrompt(taskInstructions, constraints, examples, outputRequirements);
    }

    protected override string BuildSystemRole()
    {
        return $"""
        ## SYSTEM ROLE
        You are a batch classifier for the SDK customization workflow. You analyze multiple feedback items 
        (build errors, API review comments, user requests) and classify each one.
        Your task: {Description} and determine the appropriate action path for each item.
        
        ## SAFETY GUIDELINES
        - Follow Azure SDK standards and Microsoft policies
        - Do not process or expose sensitive information (credentials, secrets, personal data)
        - Refuse requests involving sensitive data - ask for clarification if uncertain
        - Provide accurate, actionable classifications based on TypeSpec capabilities
        """;
    }

    private string BuildTaskInstructions()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"""
        **Current Context:**
        - Service: {_serviceName ?? "N/A"}
        - Language: {_language ?? "N/A"}

        **Task:**
        Classify ALL of the feedback items listed below. For each item, determine the appropriate classification: **TSP_APPLICABLE**, **SUCCESS**, or **REQUIRES_MANUAL_INTERVENTION**.
        - If the feedback is non-actionable (discussion, informational, "keep as is", or about build/generation succeeding), classify as **SUCCESS**.
        - If the feedback is actionable AND TypeSpec client customization decorators can address it (based on the reference documentation below), classify as **TSP_APPLICABLE**.
        - If the feedback is actionable but NO TypeSpec decorators can address it (requires code-level changes), classify as **REQUIRES_MANUAL_INTERVENTION**.

        Use the available tools to inspect the TypeSpec project files when needed to determine if decorators are applicable.

        **Global Context:**
        {_globalContext}

        ---

        **Feedback Items to Classify ({_items.Count} items):**
        """);

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            sb.AppendLine($"""

            --- ITEM {i + 1} ---
            ID: {item.Id}
            Text: {item.Text}
            Context: {item.Context}
            """);
        }

        sb.AppendLine($"""

        ---

        **TypeSpec Client Customizations Reference:**
        <reference_doc>
        {_referenceDocContent}
        </reference_doc>

        **Available Tools:**
        - `read_file`: Read contents of specific files in the spec repo
        - `list_dir`: List files and directories
        - `grep_search`: Search for patterns within files
        """);

        return sb.ToString();
    }

    private string BuildClassificationConditions()
    {
        return """
        **Decision Logic (apply to EACH item independently):**

        **If Context is NON-EMPTY** (check first):
        - Contains error indicators ("Failed", "error", "COMPILATION ERROR", "cannot find", "did not address") → **REQUIRES_MANUAL_INTERVENTION**
        - Contains success ("Successfully applied", "Build succeeded") → **SUCCESS**
        - Otherwise (unclear or no clear indicator) → **REQUIRES_MANUAL_INTERVENTION**

        **If Context is EMPTY** (first attempt):
        - Non-actionable (informational, "keep as is", past tense, build success, discussion, question) → **SUCCESS**
        - Actionable AND a TypeSpec decorator from the reference doc can address it → **TSP_APPLICABLE**
        - Actionable BUT no TypeSpec decorator can address it (requires code changes) → **REQUIRES_MANUAL_INTERVENTION**

        **What counts as "Non-actionable" (SUCCESS):**
        - Explicit acceptance: "Keep as is", "No changes needed", "This is fine"
        - Past tense (already done): "Method was made private", "Client was renamed"
        - Informational: Explanations, questions, acknowledgments
        - Build/generation success with no errors
        - Discussion or questions without a clear directive

        **TypeSpec Decorator Applicability (TSP_APPLICABLE):**
        Consult the reference documentation provided to determine if any supported
        TypeSpec client customization decorator can address the feedback.

        **Common feedback patterns that ARE TypeSpec-applicable:**
        - Renaming (client, operation, model, property, enum value) → `@@clientName` or `@clientName`
        - Visibility/access (make internal, hide, not public, expose publicly) → `@@access` or `@access`
        - Language-specific (exclude from Python, suppress for JS, only in .NET, not for Java) → `@@scope` with language parameter
        - Client structure (split client, merge operations, operation groups) → `@client`, `@operationGroup`
        - Client location/namespace changes → `@clientLocation`, `@clientNamespace`
        - Type overrides (use different type in SDK) → `@@alternateType`, `@@override`

        **Code Changes Required (REQUIRES_MANUAL_INTERVENTION):**
        If the feedback requires changes that TypeSpec decorators cannot handle (e.g., custom
        serialization logic, complex method implementations, test changes, documentation edits
        outside TypeSpec), classify as REQUIRES_MANUAL_INTERVENTION.
        """;
    }

    private string BuildExamples()
    {
        return """
        **Example batch output (for 3 items):**
        ```
        [abc-123]
        Classification: SUCCESS
        Reason: Explicitly states to keep as is

        [def-456]
        Classification: TSP_APPLICABLE
        Reason: @@clientName decorator can rename the client in client.tsp

        [ghi-789]
        Classification: REQUIRES_MANUAL_INTERVENTION
        Reason: Custom retry logic requires code changes; no TypeSpec decorator applies
        ```
        """;
    }

    private string BuildOutputRequirements()
    {
        return """
        **CRITICAL: Required Output Format**
        
        You MUST output one block per feedback item, using the exact item ID in square brackets as a header.
        Every item MUST appear in the output. Do NOT skip any items.

        ```
        [<item-id>]
        Classification: [TSP_APPLICABLE | SUCCESS | REQUIRES_MANUAL_INTERVENTION]
        Reason: <one-line explanation>

        [<next-item-id>]
        Classification: [TSP_APPLICABLE | SUCCESS | REQUIRES_MANUAL_INTERVENTION]
        Reason: <one-line explanation>
        ```

        **Rules:**
        - The `[<item-id>]` header MUST match the exact ID from each feedback item
        - Classification must be exactly one of: TSP_APPLICABLE, SUCCESS, or REQUIRES_MANUAL_INTERVENTION
        - Reason must clearly state which condition triggered the classification
        - For TSP_APPLICABLE: mention which TypeSpec decorator(s) can address the feedback
        - For REQUIRES_MANUAL_INTERVENTION: explain why no TypeSpec decorator applies
        - For SUCCESS: explain why the feedback is non-actionable or already resolved
        - Do NOT include Next Action or step-by-step guidance (that is handled separately)
        - Output ALL items — every single item ID must appear in your response
        - Do NOT add any text before or after the classification blocks
        """;
    }
}
