// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Sdk.Tools.Cli.Helpers.ClientCustomization;
using Azure.Sdk.Tools.Cli.Services.APIView;

namespace Azure.Sdk.Tools.Cli.Prompts.Templates;

/// <summary>
/// Template for Phase A execution - applying TypeSpec client customizations based on API review feedback.
/// Creates GitHub issues for TypeSpec customization work from pre-classified PHASE_A feedback.
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
        var taskInstructions = BuildTaskInstructions();
        var constraints = BuildConstraints();
        var outputRequirements = BuildOutputRequirements();

        return BuildStructuredPrompt(taskInstructions, constraints, outputRequirements);
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
            Apply TypeSpec client customizations to address APIView feedback comments.

            ## Current Task

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
        sb.AppendLine("| LineNo | LineId | LineText | CommentText |");
        sb.AppendLine("|--------|--------|----------|-------------|");

        foreach (var comment in _comments)
        {
            var lineText = string.IsNullOrEmpty(comment.LineText) ? "(general)" : comment.LineText;
            var lineId = string.IsNullOrEmpty(comment.LineId) ? "" : comment.LineId;
            sb.AppendLine($"| {comment.LineNo} | {lineId} | {lineText} | {comment.Comment} |");
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
            - PR description MUST include markdown table with two columns:
              | Addressed Comments | Manually Review |
              |(list lineNo + summary)|(list lineNo + reason)|
            - Comments in "Manually Review" may need clarification OR require SDK code customization if TypeSpec cannot address them
            - If any comments require SDK code customization, see language-specific guidance: {GetCodeCustomizationDocUrl(_language)}
            """;
    }

}
