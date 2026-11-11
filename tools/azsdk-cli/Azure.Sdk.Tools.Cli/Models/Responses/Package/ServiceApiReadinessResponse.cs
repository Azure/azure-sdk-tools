using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class ServiceApiReadinessResponse : CommandResponse
{
    public const string Passed = "Passed";
    public const string NotDetected = "Not detected";
    public const string Inconclusive = "Inconclusive";
    public const string Error = "Error";

    [JsonPropertyName("contractVersion")]
    public int ContractVersion { get; set; } = 1;

    [JsonPropertyName("status")]
    public string ReadinessStatus { get; set; } = Inconclusive;

    [JsonPropertyName("inputKind")]
    public string InputKind { get; set; } = "pull-request";

    [JsonPropertyName("headSha")]
    public string HeadSha { get; set; } = "";

    [JsonPropertyName("headRepository")]
    public string HeadRepository { get; set; } = "";

    [JsonPropertyName("plane")]
    public string Plane { get; set; } = "unknown";

    [JsonPropertyName("checkedAt")]
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = "";

    [JsonPropertyName("projects")]
    public List<ServiceApiReadinessProject> Projects { get; set; } = [];

    [JsonPropertyName("packages")]
    public List<PackageInfo> Packages { get; set; } = [];

    protected override string Format()
    {
        var lines = new List<string> { $"Service API-version check: {ReadinessStatus}" };
        if (!string.IsNullOrEmpty(HeadSha))
        {
            lines.Add($"Checked SDK head: {HeadRepository}@{HeadSha}");
        }
        lines.Add(Detail);
        foreach (var project in Projects)
        {
            lines.Add($"{project.Package.DirectoryPath}: {project.ReadinessStatus}. {project.Detail}");
        }
        return string.Join(Environment.NewLine, lines);
    }
}
