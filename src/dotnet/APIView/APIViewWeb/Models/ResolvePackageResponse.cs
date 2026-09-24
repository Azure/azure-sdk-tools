namespace APIViewWeb.Models;

public class ResolvePackageResponse
{
    public string PackageName { get; set; }
    public string Language { get; set; }
    public string ReviewId { get; set; }
    public string Version { get; set; }
    public string RevisionId { get; set; }
    public string RevisionLabel { get; set; }
}
