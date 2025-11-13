namespace Azure.Sdk.Tools.Cli.Models
{
    public class ParsedSdkPullRequest
    {
        public string RepoOwner { get; set; } = string.Empty;
        public string RepoName { get; set; } = string.Empty;
        public int PrNumber { get; set; } = 0;
        public string FullUrl { get; set; } = string.Empty;
        public bool IsValid => !string.IsNullOrEmpty(RepoOwner) && !string.IsNullOrEmpty(RepoName) && PrNumber > 0;
    }
}