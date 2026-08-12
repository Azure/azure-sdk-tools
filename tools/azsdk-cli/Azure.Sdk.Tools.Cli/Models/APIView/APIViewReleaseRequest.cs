namespace Azure.Sdk.Tools.Cli.Models.APIView;

public class APIViewReleaseRequest
{
    public required string SourceFilePath { get; set; }
    public string? ReviewTokenFileName { get; set; }
    public string? BuildId { get; set; }
    public string ArtifactName { get; set; } = "packages";
    public string? RepoName { get; set; }
    public required string PackageName { get; set; }
    public string Project { get; set; } = "internal";
    public required string PackageVersion { get; set; }
    public required string PackageType { get; set; }
    public string? SourceBranch { get; set; }
}