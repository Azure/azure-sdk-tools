// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for classifying SDK feedback and routing them to the appropriate phase.
/// Supports batch classification of multiple feedback items in a single LLM call,
/// with strictly formatted ID-keyed output for robust parsing.
/// </summary>
public class FeedbackClassificationTemplate : ClassificationBaseTemplate<FeedbackItemClassificationDetails, FeedbackItem>
{
    public override string TemplateId => "feedback-classification";
    public override string Version => "1.0.0";
    public override string Description => "Classify SDK feedback items in batch and route to appropriate phase";

    private readonly string? _serviceName;
    private readonly string _language;
    private readonly string _referenceDocContent;
    private readonly List<FeedbackItem> _items;
    private readonly string _globalContext;
    private readonly EditScope _editScope;
    private static readonly Regex BatchResultBlockPattern = new(
        @"\[(?<id>[^\]]+)\]\s*\n\s*Classification:\s*(?<classification>\S+)\s*\n\s*Reason:\s*(?<reason>[^\n]+)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new batch classification template.
    /// </summary>
    /// <param name="serviceName">The name of the service being customized (optional)</param>
    /// <param name="language">Target SDK language (e.g., python, csharp, java)</param>
    /// <param name="referenceDocContent">Content of the customizing-client-tsp.md reference document</param>
    /// <param name="items">The feedback items to classify</param>
    /// <param name="globalContext">Global context containing all changes and history</param>
    /// <param name="editScope">
    /// The edit scope the classification will operate under. When an axis is out of scope, the classifier
    /// is asked to prefer the in-scope axis for items that could be addressed either way (e.g. a rename
    /// achievable in spec inputs OR custom code), so fixable items are not reported as out of scope.
    /// </param>
    public FeedbackClassificationTemplate(
        string? serviceName,
        string language,
        string referenceDocContent,
        List<FeedbackItem> items,
        string globalContext,
        EditScope editScope = EditScope.All)
    {
        _serviceName = serviceName;
        _language = language;
        _referenceDocContent = referenceDocContent;
        _items = items;
        _globalContext = globalContext;
        _editScope = editScope;
    }

    public override string BuildPrompt()
    {
        var taskInstructions = BuildTaskInstructions();
        var constraints = BuildClassificationConditions();
        var examples = BuildExamples();
        var outputRequirements = BuildOutputRequirements();
        
        return BuildStructuredPrompt(taskInstructions, constraints, examples, outputRequirements);
    }

    /// <summary>
    /// Parses the batch LLM result with ID-keyed blocks and applies classifications to items.
    /// Expected format:
    /// [item-id]
    /// Classification: TSP_APPLICABLE | SUCCESS | CODE_CUSTOMIZATION | REQUIRES_MANUAL_INTERVENTION
    /// Reason: explanation
    /// </summary>
    public override List<FeedbackItemClassificationDetails> ParseClassifyResult(string result, List<FeedbackItem>? items = null)
    {
        if (items == null || items.Count == 0)
        {
            throw new ArgumentException("Items list cannot be null or empty for classification parsing.");
        }
        var feedbackItems = items.Cast<FeedbackItem>().ToList();
        var itemLookup = feedbackItems.ToDictionary(i => i.Id, i => i);
        var matchedIds = new HashSet<string>();

        foreach (Match match in BatchResultBlockPattern.Matches(result))
        {
            var id = match.Groups["id"].Value.Trim();
            var classification = match.Groups["classification"].Value.Trim();
            var reason = match.Groups["reason"].Value.Trim();

            if (!itemLookup.TryGetValue(id, out var item))
            {
                continue;
            }

            matchedIds.Add(id);
            ApplyClassification(item, classification, reason);
        }

        // Handle any items that weren't in the response
        foreach (var item in feedbackItems.Where(i => !matchedIds.Contains(i.Id)))
        {
            item.Status = FeedbackStatus.REQUIRES_MANUAL_INTERVENTION;
            item.AppendContext("Classification failed: item missing from batch LLM response", leadingNewLines: 1);
        }

        return BuildClassificationItems(feedbackItems);
    }

    private static List<FeedbackItemClassificationDetails> BuildClassificationItems(List<FeedbackItem> items)
    {
        var successCount = 0;
        var failureCount = 0;
        var tspApplicableCount = 0;
        var codeCustomizationCount = 0;
        var classifications = new List<FeedbackItemClassificationDetails>();

        foreach (var item in items)
        {
            var classification = item.Status switch
            {
                FeedbackStatus.SUCCESS => "SUCCESS",
                FeedbackStatus.CODE_CUSTOMIZATION => "CODE_CUSTOMIZATION",
                FeedbackStatus.REQUIRES_MANUAL_INTERVENTION => "REQUIRES_MANUAL_INTERVENTION",
                _ => "TSP_APPLICABLE"
            };

            switch (item.Status)
            {
                case FeedbackStatus.SUCCESS: successCount++; break;
                case FeedbackStatus.CODE_CUSTOMIZATION: codeCustomizationCount++; break;
                case FeedbackStatus.REQUIRES_MANUAL_INTERVENTION: failureCount++; break;
                default: tspApplicableCount++; break;
            }

            classifications.Add(new FeedbackItemClassificationDetails
            {
                ItemId = item.Id,
                Classification = classification,
                Reason = item.ClassificationReason ?? $"Item classified as {classification}",
                Text = item.Text
            });
        }

        return classifications;
    }

    /// <summary>
    /// Applies a classification string and reason to a single feedback item.
    /// </summary>
    private void ApplyClassification(FeedbackItem item, string classification, string reason)
    {
        var status = classification switch
        {
            "SUCCESS" => FeedbackStatus.SUCCESS,
            "CODE_CUSTOMIZATION" => FeedbackStatus.CODE_CUSTOMIZATION,
            "REQUIRES_MANUAL_INTERVENTION" => FeedbackStatus.REQUIRES_MANUAL_INTERVENTION,
            "TSP_APPLICABLE" => FeedbackStatus.TSP_APPLICABLE,
            _ => FeedbackStatus.TSP_APPLICABLE
        };

        item.Status = status;
        if (!string.IsNullOrEmpty(reason))
        {
            item.ClassificationReason = reason;
            item.AppendContext($"Classification: {classification}\nReason: {reason}", leadingNewLines: 2);
        }
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
        - Language: {_language}
        """);

        var scopeGuidance = BuildEditScopeGuidance();
        if (!string.IsNullOrEmpty(scopeGuidance))
        {
            sb.AppendLine();
            sb.AppendLine(scopeGuidance);
        }

        sb.AppendLine($"""

        **Task:**
        Classify ALL of the feedback items listed below. For each item, determine the appropriate classification: **TSP_APPLICABLE**, **CODE_CUSTOMIZATION**, **SUCCESS**, or **REQUIRES_MANUAL_INTERVENTION**.
        - If the feedback is non-actionable (discussion, informational, "keep as is", or about build/generation succeeding), classify as **SUCCESS**.
        - If the feedback is actionable AND TypeSpec client customization decorators can address it (based on the reference documentation below), classify as **TSP_APPLICABLE**.
        - If the feedback is actionable, TypeSpec decorators CANNOT address it, but automated code patching could fix it (e.g., compile errors from method signature changes, parameter additions/removals, symbol renames in generated code), classify as **CODE_CUSTOMIZATION**. Include specific repair instructions in the Reason.
        - If the feedback is actionable but requires complex manual work that cannot be automated (e.g., new feature implementation, architectural changes, custom business logic), classify as **REQUIRES_MANUAL_INTERVENTION**.

        **IMPORTANT — Check for already-applied customizations:**
        Before classifying any item as TSP_APPLICABLE, use the available tools to search the TypeSpec project files (e.g., `client.tsp`, `customizations.tsp`) for the decorator or customization that would address the feedback. If the customization is already present in the TypeSpec files, classify the item as **SUCCESS** (the change has already been applied). Use `grep_search` or `read_file` to verify.

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

    /// <summary>
    /// Builds scope-aware guidance that biases the classifier toward the in-scope axis for items that
    /// could be addressed either way. Returns empty when both axes are in scope (<see cref="EditScope.All"/>).
    /// </summary>
    private string BuildEditScopeGuidance()
    {
        var specInScope = _editScope.HasFlag(EditScope.SpecInputs);
        var customCodeInScope = _editScope.HasFlag(EditScope.CustomCode);

        // Both axes in scope (All): no bias — classify on technical merit.
        if (specInScope && customCodeInScope)
        {
            return string.Empty;
        }

        // Custom-code-only: spec inputs cannot be edited, so prefer CODE_CUSTOMIZATION for ambiguous items.
        if (customCodeInScope && !specInScope)
        {
            return """
            **EDIT SCOPE — CUSTOM CODE ONLY (spec inputs are OUT OF SCOPE):**
            Spec inputs (`client.tsp`, `tspconfig.yaml`) and the pinned spec commit will NOT be edited in this run.
            Many fixes — especially renames, visibility/access changes, and type overrides — can be achieved EITHER
            via a TypeSpec decorator OR directly in custom (customization) code.
            - For any item that can be addressed in custom code, classify it as **CODE_CUSTOMIZATION** (NOT TSP_APPLICABLE),
              even if a TypeSpec decorator could also address it. Include specific custom-code repair instructions in the Reason.
            - Only classify an item as **TSP_APPLICABLE** when the fix genuinely CANNOT be expressed in custom code and
              truly requires a spec-input change. Such items will be reported as out of scope (a separate spec-repo PR),
              so use TSP_APPLICABLE sparingly and only when unavoidable.
            """;
        }

        // Spec-inputs-only: custom code cannot be edited, so prefer TSP_APPLICABLE for ambiguous items.
        if (specInScope && !customCodeInScope)
        {
            return """
            **EDIT SCOPE — SPEC INPUTS ONLY (custom code is OUT OF SCOPE):**
            Custom (customization) code will NOT be edited in this run.
            Many fixes — especially renames, visibility/access changes, and type overrides — can be achieved EITHER
            via a TypeSpec decorator OR directly in custom code.
            - For any item that can be addressed with a TypeSpec decorator, classify it as **TSP_APPLICABLE**
              (NOT CODE_CUSTOMIZATION), even if a custom-code change could also address it.
            - Only classify an item as **CODE_CUSTOMIZATION** when the fix genuinely CANNOT be expressed via a TypeSpec
              decorator and truly requires a custom-code change. Such items will be reported as out of scope, so use
              CODE_CUSTOMIZATION sparingly and only when unavoidable.
            """;
        }

        return string.Empty;
    }

    private string BuildClassificationConditions()
    {
        return """
        **Decision Logic (apply to EACH item independently):**

        **If Context is NON-EMPTY** (check first):
        - Contains error indicators ("Failed", "error", "COMPILATION ERROR", "cannot find", "did not address") → **CODE_CUSTOMIZATION** (if patching can fix) or **REQUIRES_MANUAL_INTERVENTION** (if too complex)
        - Contains success ("Successfully applied", "Build succeeded") → **SUCCESS**
        - Otherwise (unclear or no clear indicator) → **REQUIRES_MANUAL_INTERVENTION**

        **If Context is EMPTY** (first attempt):
        - Non-actionable (informational, "keep as is", past tense, build success, discussion, question) → **SUCCESS**
        - Actionable AND a TypeSpec decorator from the reference doc could address it → **check the TypeSpec files first** using `grep_search` or `read_file`
          - If the decorator/customization is already present in the TypeSpec files → **SUCCESS** (already applied)
          - If not present → **TSP_APPLICABLE**
        - Actionable, no TypeSpec decorator applies, but automated patching can fix (compile errors, signature changes, parameter additions/removals, symbol renames, linting or typing errors) → **CODE_CUSTOMIZATION**
        - Actionable BUT requires complex manual implementation → **REQUIRES_MANUAL_INTERVENTION**

        **What counts as "Non-actionable" (SUCCESS):**
        - Explicit acceptance: "Keep as is", "No changes needed", "This is fine"
        - Past tense (already done): "Method was made private", "Client was renamed"
        - Already applied in TypeSpec: the requested customization decorator is already present in `client.tsp` or another TypeSpec customization file (verified by reading/searching the files)
        - Informational: Explanations, questions, acknowledgments
        - Build/generation success with no errors
        - Discussion or questions without a clear directive

        **TypeSpec Decorator Applicability (TSP_APPLICABLE):**
        Consult the reference documentation provided to determine if any supported
        TypeSpec client customization decorator can address the feedback.
        **Always search the TypeSpec files first** (using `grep_search` or `read_file`) to confirm the decorator
        is NOT already present before classifying as TSP_APPLICABLE. If it is already present, classify as SUCCESS.

        **Common feedback patterns that ARE TypeSpec-applicable:**
        - Renaming (client, operation, model, property, enum value) → `@@clientName` or `@clientName`
        - Visibility/access (make internal, hide, not public, expose publicly) → `@@access` or `@access`
        - Language-specific (exclude from Python, suppress for JS, only in .NET, not for Java) → `@@scope` with language parameter
        - Client structure (split client, merge operations, operation groups) → `@client`, `@operationGroup`
        - Client location/namespace changes → `@clientLocation`, `@clientNamespace`
        - Type overrides (use different type in SDK) → `@@alternateType`, `@@override`

        **Code Changes Required (REQUIRES_MANUAL_INTERVENTION):**
        If the feedback requires complex changes that neither TypeSpec decorators nor automated patching can handle
        (e.g., new feature implementation, architectural redesign, custom business logic,
        test changes, documentation edits outside TypeSpec), classify as REQUIRES_MANUAL_INTERVENTION.

        **Automated Code Patching (CODE_CUSTOMIZATION):**
        If the feedback involves compile errors or straightforward code-level fixes that automated patching
        can handle (e.g., method signature changes, parameter additions/removals, symbol renames, linting or typing errors),
        classify as CODE_CUSTOMIZATION.

        Build errors often reference GENERATED files, but those must NOT be edited directly — they
        are regenerated from TypeSpec. The root cause is typically in a customization file that
        references a renamed or removed symbol. In your Reason, identify the failing symbol and
        what changed, but do NOT instruct editing the generated file. The automated patch agent
        will locate and fix the correct customization file.
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
        Classification: CODE_CUSTOMIZATION
        Reason: Method `buildDocumentModelRequest` in the customization file needs a new `options` parameter of type `BuildDocumentModelOptions` to match the updated generated signature.

        [jkl-012]
        Classification: REQUIRES_MANUAL_INTERVENTION
        Reason: Requires implementing new retry policy with custom business logic; no TypeSpec decorator or automated patch applies
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
        Classification: [TSP_APPLICABLE | CODE_CUSTOMIZATION | SUCCESS | REQUIRES_MANUAL_INTERVENTION]
        Reason: <one-line explanation — for CODE_CUSTOMIZATION, include specific repair instructions>

        [<next-item-id>]
        Classification: [TSP_APPLICABLE | CODE_CUSTOMIZATION | SUCCESS | REQUIRES_MANUAL_INTERVENTION]
        Reason: <one-line explanation>
        ```

        **Rules:**
        - The `[<item-id>]` header MUST match the exact ID from each feedback item
        - Classification must be exactly one of: TSP_APPLICABLE, CODE_CUSTOMIZATION, SUCCESS, or REQUIRES_MANUAL_INTERVENTION
        - Reason must clearly state which condition triggered the classification
        - For TSP_APPLICABLE: mention which TypeSpec decorator(s) can address the feedback
        - For CODE_CUSTOMIZATION: explain what code changes are needed with specific repair instructions in the Reason
        - For REQUIRES_MANUAL_INTERVENTION: explain why no TypeSpec decorator or automated patch applies
        - For SUCCESS: explain why the feedback is non-actionable or already resolved
        - Do NOT include Next Action or step-by-step guidance (that is handled separately)
        - Output ALL items — every single item ID must appear in your response
        - Do NOT add any text before or after the classification blocks
        """;
    }
}
