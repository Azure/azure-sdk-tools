using Octokit;

namespace GitHubIssues.Models
{
    public class ReportIssue
    {
        public Issue Issue { get; set; }

        public Milestone Milestone { get; set; }

        public string Note { get; set; }
    }
}
