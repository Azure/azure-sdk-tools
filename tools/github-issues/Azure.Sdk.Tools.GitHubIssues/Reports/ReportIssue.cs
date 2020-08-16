using Octokit;

namespace Azure.Sdk.Tools.GitHubIssues.Reports
{
    public class ReportIssue
    {
        public Issue Issue { get; set; }

        public Milestone Milestone { get; set; }

        public string Note { get; set; }
    }
}
