// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Sdk.Tools.Cli.Services.APIView;

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for Phase A execution - applying TypeSpec client customizations based on API review feedback.
/// Creates GitHub issues for TypeSpec customization work from pre-classified PHASE_A feedback.
/// </summary>
public class TypeSpecCustomizationTemplate : BasePromptTemplate
{
    public override string TemplateId => "typespec-customization";
    public override string Version => "1.0.0";
    public override string Description => "Apply TypeSpec client customizations from API review feedback";

    private readonly string _serviceName;
    private readonly string _language;
    private readonly string? _apiViewUrl;
    private readonly string _rawFeedback;
    private readonly IReadOnlyList<EnrichedApiViewComment>? _enrichedComments;

    /// <summary>
    /// Initializes a new TypeSpec customization template with the specified parameters.
    /// </summary>
    /// <param name="serviceName">The name of the service being customized</param>
    /// <param name="language">Target SDK language (e.g., python, csharp, java)</param>
    /// <param name="apiViewUrl">Optional APIView URL for reference</param>
    /// <param name="rawFeedback">The raw feedback text</param>
    /// <param name="enrichedComments">Pre-enriched APIView comments, if available</param>
    public TypeSpecCustomizationTemplate(
        string serviceName,
        string language,
        string? apiViewUrl,
        string rawFeedback,
        IReadOnlyList<EnrichedApiViewComment>? enrichedComments = null)
    {
        _serviceName = serviceName;
        _language = language;
        _apiViewUrl = apiViewUrl;
        _rawFeedback = rawFeedback;
        _enrichedComments = enrichedComments;
    }

    /// <summary>
    /// Builds the complete TypeSpec customization prompt.
    /// </summary>
    public override string BuildPrompt()
    {
        var taskInstructions = BuildTaskInstructions();
        var constraints = BuildConstraints();
        var examples = BuildExamples();
        var outputRequirements = BuildOutputRequirements();

        return BuildStructuredPrompt(taskInstructions, constraints, examples, outputRequirements);
    }

    private string BuildTaskInstructions()
    {
        var feedbackSection = FormatFeedback();

        return $"""
            You create GitHub issues for TypeSpec client customization work based on pre-classified PHASE_A feedback.

            ## Current Task

            **Service**: {_serviceName}
            **Language**: {_language}
            **APIView URL**: {_apiViewUrl ?? "N/A"}
            **Reference**: https://github.com/Azure/azure-rest-api-specs/blob/main/eng/common/knowledge/customizing-client-tsp.md

            ## Feedback to Address

            {feedbackSection}
            """;
    }

    private string FormatFeedback()
    {
        if (_enrichedComments == null || _enrichedComments.Count == 0)
        {
            return _rawFeedback;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"### APIView Comments ({_enrichedComments.Count} unresolved)");
        sb.AppendLine();
        sb.AppendLine("| line_text | comment_text |");
        sb.AppendLine("|-----------|--------------|");

        foreach (var comment in _enrichedComments)
        {
            var lineText = string.IsNullOrEmpty(comment.LineText) ? "(general)" : $"`{comment.LineText}`";
            sb.AppendLine($"| {lineText} | {comment.CommentText} |");
        }

        return sb.ToString();
    }

    private static string BuildConstraints()
    {
        return """
            ## Critical Rules

            1. **Ask for Permission**: Before creating any GitHub issue, you MUST:
               - Show the user the formatted issue title and body
               - Ask: "Do you want me to create this issue in Azure/azure-rest-api-specs? (yes/no)"
               - Only proceed with issue creation if the user explicitly confirms

            2. **Issue Repository**: Issues are created in Azure/azure-rest-api-specs

            3. **Assign Copilot**: After creating the issue, assign Copilot to it so it starts working on a PR automatically.
            """;
    }

    private string BuildExamples()
    {
        return $"""
            ## Issue Title Format

            ```
            TESTING: [SDK Customization] Apply TypeSpec client customizations for <ServiceName> (<Language>)
            ```

            Example: `TESTING: [SDK Customization] Apply TypeSpec client customizations for Widget ({_language})`
            """;
    }

    private static string BuildOutputRequirements()
    {
        return """
            ## Output

            Generate a GitHub issue with this structure:

            ### Issue Title
            ```
            TESTING: [SDK Customization] Apply TypeSpec client customizations for {ServiceName} ({Language})
            ```

            ### Issue Body
            ```markdown
            ## Overview

            Apply TypeSpec client customizations to address API review feedback.

            ## Context

            - **Service**: {ServiceName}
            - **Language**: {Language}
            - **APIView**: {APIViewURL}
            - **Reference**: [TypeSpec Client Customizations Guide](https://github.com/Azure/azure-rest-api-specs/blob/main/eng/common/knowledge/customizing-client-tsp.md)

            ## Feedback to Address

            {For each comment, format as a table row:}

            | line_text | comment_text |
            |-----------|--------------|
            | `{line_text}` | {comment_text} |

            ## Validation

            After applying changes:
            1. Run `tsp compile .` - ensure no errors
            2. Regenerate SDK and verify names/visibility changed as expected

            ---
            *Auto-generated by SDK customization tool*
            ```

            After showing the issue to the user, ask for confirmation before creating.
            """;
    }
}
