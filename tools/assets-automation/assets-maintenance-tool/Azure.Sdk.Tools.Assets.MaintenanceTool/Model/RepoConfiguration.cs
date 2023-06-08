namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model;

/// <summary>
/// Configuration class describing options available while targeting a repository for scanning.
/// </summary>
public class RepoConfiguration
{
    public RepoConfiguration(string repo)
    {
        LanguageRepo = repo;
    }

    public RepoConfiguration() {
        LanguageRepo = string.Empty;
    }

    /// <summary>
    /// The full orgname/repo-id identifier to access a repo on github. EG: "azure/azure-sdk-for-net"
    /// </summary>
    public string LanguageRepo { get; set; }

    /// <summary>
    /// The time from which we will search for commits that contain assets.jsons. The current default was chosen
    /// almost arbitrarily. Official test-proxy began supported external assets in late November of 2022, so we don't
    /// need to go further back then that when examining the SHAs in the language repos. There is no possibility of an
    /// assets.json past this date!
    /// </summary>
    public DateTime ScanStartDate { get; set; } = DateTime.Parse("2022-12-01");

    /// <summary>
    /// The set of branches that we will examine. Defaults to just 'main'.
    /// </summary>
    public List<string> Branches { get; set; } = new List<string> { "main" };
}
