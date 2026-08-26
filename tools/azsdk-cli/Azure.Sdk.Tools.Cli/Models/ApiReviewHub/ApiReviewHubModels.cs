using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Models.ApiReviewHub;

public class GitBranchReference
{
    [JsonPropertyName("owner")]
    public required string Owner { get; set; }

    [JsonPropertyName("repo")]
    public required string Repo { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

public class ReviewPullRequestCreationRequest
{
    [JsonPropertyName("language")]
    public required string Language { get; set; }

    [JsonPropertyName("packageName")]
    public required string PackageName { get; set; }

    [JsonPropertyName("baseTag")]
    public string BaseTag { get; set; } = string.Empty;

    [JsonPropertyName("targetBranch")]
    public required GitBranchReference TargetBranch { get; set; }
}

public class MarkPackageReleasedRequest
{
    public required string Language { get; set; }
    public required string PackageName { get; set; }
    public required string Version { get; set; }
    public required string ApiHash { get; set; }
    public string RepoOwner { get; set; } = string.Empty;
    public required DateTimeOffset ReleasedOn { get; set; }
    public bool DryRun { get; set; } = true;
}

public class ApiReviewHubMarkReleasedResult
{
    public Guid PackageId { get; set; }
    public Guid PackageVersionId { get; set; }
    public required string PackageName { get; set; }
    public required string Language { get; set; }
    public required string Version { get; set; }
    public required string ReleasedApiHash { get; set; }
    public required string ApprovalStatus { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApprovalRecordId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AppliedInheritanceRule { get; set; }
    public bool IsReleased { get; set; }
    public DateTimeOffset? ReleasedOn { get; set; }
}

public class ReviewPullRequestCreationAcceptedResponse
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class OperationStatus
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; set; }

    [JsonPropertyName("language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }

    [JsonPropertyName("packageName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PackageName { get; set; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("pipelineUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PipelineUrl { get; set; }

    [JsonPropertyName("reviewPullRequest")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? ReviewPullRequest { get; set; }

    [JsonPropertyName("failureReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FailureReason { get; set; }
}

public class ApiReviewHubApprovalRecord
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("apiHash")]
    public string ApiHash { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("commitSha")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CommitSha { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("pullRequestUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PullRequestUrl { get; set; }

    [JsonPropertyName("lastUpdatedBy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastUpdatedBy { get; set; }

    [JsonPropertyName("lastUpdatedOn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastUpdatedOn { get; set; }
}

public class ApiReviewHubReleaseGateResult
{
    [JsonPropertyName("statusCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StatusCode { get; set; }

    [JsonPropertyName("isApproved")]
    public bool IsApproved { get; set; }

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }

    [JsonPropertyName("appliedInheritanceRule")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AppliedInheritanceRule { get; set; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Details { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    [JsonPropertyName("approvals")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ApiReviewHubApprovalRecord>? Approvals { get; set; } = [];
}

public class ApiViewReleaseStatusResult
{
    [JsonPropertyName("statusCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? StatusCode { get; set; }

    [JsonPropertyName("isApproved")]
    public bool IsApproved { get; set; }

    [JsonPropertyName("packageNameApproved")]
    public bool PackageNameApproved { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public IReadOnlyList<string> Details { get; set; } = [];

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}

public class PackageReleaseStatusResult
{
    [JsonPropertyName("isApproved")]
    public bool IsApproved { get; set; }

    [JsonPropertyName("finalSource")]
    public string FinalSource { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("reviewHub")]
    public ApiReviewHubReleaseGateResult ReviewHub { get; set; } = new();

    [JsonPropertyName("apiView")]
    public ApiViewReleaseStatusResult? ApiView { get; set; }
}

public class ApiReviewHubResponse : CommandResponse
{
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OperationStatus? Result { get; set; }

    protected override string Format()
    {
        var output = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(Result?.Message))
        {
            output.AppendLine(Result.Message);
        }

        if (Result != null)
        {
            var prUrl = TryGetReviewPullRequestUrl(Result.ReviewPullRequest);
            if (!string.IsNullOrWhiteSpace(prUrl))
            {
                output.AppendLine($"Review PR: {prUrl}");
            }
        }

        return output.ToString();
    }

    private static string? TryGetReviewPullRequestUrl(JsonElement? reviewPullRequest)
    {
        if (reviewPullRequest is null || reviewPullRequest.Value.ValueKind != JsonValueKind.Object)
        {
            if (reviewPullRequest is null || reviewPullRequest.Value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return reviewPullRequest.Value.GetString();
        }

        if (reviewPullRequest.Value.TryGetProperty("url", out var urlElement) &&
            urlElement.ValueKind == JsonValueKind.String)
        {
            return urlElement.GetString();
        }

        return null;
    }
}

public class PackageReleaseStatusResponse : CommandResponse
{
    private const string Yellow = "\u001b[33m";
    private const string Red = "\u001b[31m";
    private const string Reset = "\u001b[0m";

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PackageReleaseStatusResult? Result { get; set; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Details { get; set; }

    protected override string Format()
    {
        return Details == null ? string.Empty : string.Join(Environment.NewLine, Details);
    }

    public override string ToString()
    {
        var messages = new List<string>();
        if (Details?.Count > 0)
        {
            messages.AddRange(FormatDetailsForPlainText(Details));
        }

        if (!string.IsNullOrEmpty(ResponseError))
        {
            AddBlankLine(messages);
            messages.Add(Colorize("[ERROR] " + ResponseError, Red));
        }

        foreach (var error in ResponseErrors ?? [])
        {
            AddBlankLine(messages);
            messages.Add(Colorize("[ERROR] " + error, Red));
        }

        if (NextSteps?.Count > 0)
        {
            AddBlankLine(messages);
            messages.Add("[NEXT STEPS]");
            messages.AddRange(NextSteps);
        }

        if (SupportChannel != null)
        {
            AddBlankLine(messages);
            messages.Add(SupportChannel);
        }

        return string.Join(Environment.NewLine, messages);
    }

    private static List<string> FormatDetailsForPlainText(IReadOnlyList<string> details)
    {
        var output = new List<string>();
        var inSection = false;

        foreach (var detail in details)
        {
            if (IsSectionHeader(detail))
            {
                AddBlankLine(output);
                output.Add(detail);
                output.Add(string.Empty);
                inSection = true;
                continue;
            }

            var line = inSection ? $"  {detail}" : detail;
            output.Add(detail.StartsWith("WARNING:", StringComparison.Ordinal) ? Colorize(line, Yellow) : line);
        }

        return output;
    }

    private static string Colorize(string value, string color)
    {
        return Environment.GetEnvironmentVariable("NO_COLOR") == null
            ? $"{color}{value}{Reset}"
            : value;
    }

    private static bool IsSectionHeader(string value) =>
        value.StartsWith("==", StringComparison.Ordinal) && value.EndsWith("==", StringComparison.Ordinal);

    private static void AddBlankLine(List<string> lines)
    {
        if (lines.Count > 0 && lines[^1] != string.Empty)
        {
            lines.Add(string.Empty);
        }
    }
}