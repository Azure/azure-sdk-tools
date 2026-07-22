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
    public required string BaseTag { get; set; }

    [JsonPropertyName("targetBranch")]
    public required GitBranchReference TargetBranch { get; set; }
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

public class ApiReviewHubRequestReviewPullRequestResult
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OperationStatus? Operation { get; set; }
}

public class ApiReviewHubResponse : CommandResponse
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true
    };

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiReviewHubRequestReviewPullRequestResult? Result { get; set; }

    protected override string Format()
    {
        var output = new StringBuilder();
        if (!string.IsNullOrEmpty(Message))
        {
            output.AppendLine(Message);
        }

        if (Result != null)
        {
            output.AppendLine(JsonSerializer.Serialize(Result, serializerOptions));
        }

        return output.ToString();
    }
}

public class ApiReviewReleaseStatusResponse : CommandResponse
{
    private const string Yellow = "\u001b[33m";
    private const string Red = "\u001b[31m";
    private const string Reset = "\u001b[0m";

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Details { get; set; }

    [JsonIgnore]
    public override bool WriteToStdoutOnFailure => true;

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