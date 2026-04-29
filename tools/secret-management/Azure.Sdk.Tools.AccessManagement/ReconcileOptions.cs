namespace Azure.Sdk.Tools.AccessManagement;

public class ReconcileOptions
{
    public bool NoDelete { get; set; }
    public bool DryRun { get; set; }
    public bool NoGitHubSecrets { get; set; }
}
