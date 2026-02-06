namespace Azure.Sdk.Tools.Cli.Models;

public class ResolvePackageResponse
{
    public string PackageName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string ReviewId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    public string RevisionLabel { get; set; } = string.Empty;
}
