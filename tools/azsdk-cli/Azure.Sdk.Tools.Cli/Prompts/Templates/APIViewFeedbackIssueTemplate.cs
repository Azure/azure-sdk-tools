// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// GitHub issue prompt for resolving APIView feedback with TypeSpec customization.
/// </summary>
public class APIViewFeedbackIssueTemplate : BasePromptTemplate
{
    public override string TemplateId => "apiview-feedback-issue";
    public override string Version => "1.0.0";
    public override string Description => "Apply TypeSpec client customizations from API review feedback";

    private readonly string _packageName;
    private readonly string _language;
    private readonly string? _apiViewUrl;
    private readonly List<ConsolidatedComment> _comments;
    private readonly string? _commitSha;
    private readonly string? _tspProjectPath;

    /// <summary>
    /// Initializes a new TypeSpec customization template with the specified parameters.
    /// </summary>
    /// <param name="packageName">The name of the package being customized</param>
    /// <param name="language">Target SDK language (e.g., python, csharp, java)</param>
    /// <param name="apiViewUrl">APIView URL for reference</param>
    /// <param name="comments">Consolidated APIView comments to address</param>
    /// <param name="commitSha">Optional: Commit SHA for the TypeSpec changes</param>
    /// <param name="tspProjectPath">Optional: Path to the TypeSpec project directory</param>
    public APIViewFeedbackIssueTemplate(
        string packageName,
        string language,
        string apiViewUrl,
        List<ConsolidatedComment> comments,
        string? commitSha = null,
        string? tspProjectPath = null)
    {
        _packageName = packageName;
        _language = language;
        _apiViewUrl = apiViewUrl;
        _comments = comments;
        _commitSha = commitSha;
        _tspProjectPath = tspProjectPath;
    }

    private static string GetCodeCustomizationDocUrl(string language) => language.ToLowerInvariant() switch
    {
        "python" => "https://github.com/Azure/autorest.python/blob/main/docs/customizations.md",
        "java" => "https://github.com/Azure/autorest.java/blob/main/customization-base/README.md",
        "dotnet" => "https://github.com/microsoft/typespec/blob/main/packages/http-client-csharp/.tspd/docs/customization.md",
        "go" => "https://github.com/Azure/azure-sdk-for-go/blob/main/documentation/development/generate.md",
        "javascript" or "typescript" => "https://github.com/Azure/azure-sdk-for-js/wiki/Modular-(DPG)-Customization-Guide",
        _ => "https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/knowledge/customizing-client-tsp.md"
    };

    /// <summary>
    /// Builds the complete TypeSpec customization prompt.
    /// </summary>
    public override string BuildPrompt()
    {
        var sb = new StringBuilder();

        sb.AppendLine(BuildTaskInstructions());
        sb.AppendLine();
        sb.AppendLine("## Constraints");
        sb.AppendLine();
        sb.AppendLine(BuildConstraints());
        sb.AppendLine();
        sb.AppendLine("## Output Requirements");
        sb.AppendLine();
        sb.AppendLine(BuildOutputRequirements());

        return sb.ToString();
    }

    private string BuildTaskInstructions()
    {
        var feedbackSection = FormatFeedback();
        
        var shaSection = !string.IsNullOrEmpty(_commitSha)
            ? $"**Commit SHA**: {_commitSha}\n"
            : string.Empty;
            
        var tspPathSection = !string.IsNullOrEmpty(_tspProjectPath)
            ? $"**TypeSpec Project Path**: {_tspProjectPath}\n"
            : string.Empty;

        return $"""
            # Current Task

            Apply TypeSpec client customizations to address APIView feedback comments.

            **Package Name**: {_packageName}
            **Language**: {_language}
            **APIView URL**: {_apiViewUrl ?? "N/A"}
            {shaSection}{tspPathSection}
            ## Feedback to Address

            {feedbackSection}
            """;
    }

    private string FormatFeedback()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### APIView Comments ({_comments.Count} unresolved)");
        sb.AppendLine();
        sb.AppendLine("| LineNo | Element | LineText | CommentText |");
        sb.AppendLine("|--------|---------|----------|-------------|");

        foreach (var comment in _comments)
        {
            var lineText = (comment.LineText ?? "").Replace("|", "\\|").Replace("\r", "").Replace("\n", " ").Trim();
            var lineId = (comment.LineId ?? "").Replace("|", "\\|");
            var commentText = (comment.Comment ?? "").Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
            sb.AppendLine($"| {comment.LineNo} | {lineId} | {lineText} | {commentText} |");
        }

        return sb.ToString();
    }

    private string BuildConstraints()
    {
        return """
            - Apply TypeSpec client customizations to resolve as many comments as possible
            - MUST consult: https://github.com/Azure/azure-rest-api-specs/blob/main/eng/common/knowledge/customizing-client-tsp.md
            """;
    }

    private string BuildOutputRequirements()
    {
        return $"""
            - If a Commit SHA is provided, use it as the base for your changes
            - Include the APIView URL in PR description
            - Update ONLY the client.tsp file(s) for the spec directory referenced by this APIView. Do not modify other spec directories, even if feedback mentions related namespaces.
              
              Example of feedback that should only update `specification/ai/Azure.AI.Projects/client.tsp`:
              - Package Name: azure-ai-projects
                - LineNo 227: `azure.ai.projects.aio.operations.AgentsOperations.delete:async.returntype` | Return 'None' on delete.
                - LineNo 10120: `azure.ai.projects.telemetry.trace_function` | Does this need to be a public function?
              
              These should NOT also update `specification/ai/Azure.AI.Agents/client.tsp` even though they mention Agents-related elements.
            
            - CRITICAL: You MUST update the PR description with a summary table. The PR description MUST include a markdown table in EXACTLY this format (do not change column names):
              | LineNo | Addressed? | Summary |
              |--------|------------|---------|
              | <lineNo> | ✅ | Brief description of changes (or "No action needed" if feedback says keep as-is) |
              | <lineNo> | ⚠️ | Reason not addressed (unclear info, TypeSpec limitation, needs SDK code customization) |
              
              Include one row for EVERY LineNo from the feedback table above. This table is required even if the PR is long.

            - Note: If a review comment CANNOT be addressed, explanation comments MUST NOT be added to the `client.tsp` file.
              ONLY explain in the "Summary" column why it could not be addressed.
            - Include SDK code customization guidance: {GetCodeCustomizationDocUrl(_language)}

            ---
            *Auto-generated by DelegateAPIViewFeedbackTool*
            """;
    }

}
