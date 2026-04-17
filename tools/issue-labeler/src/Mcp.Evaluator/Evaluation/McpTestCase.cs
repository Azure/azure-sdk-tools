using IssueLabeler.Shared;

namespace Mcp.Evaluator.Evaluation
{
    /// <summary>
    /// Represents a test case with ground truth labels for evaluation
    /// </summary>
    public class McpTestCase
    {
        public int IssueNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string ExpectedServerLabel { get; set; } = string.Empty;
        public string ExpectedToolLabel { get; set; } = string.Empty;
        public string? Notes { get; set; }

        public IssuePayload ToIssuePayload()
        {
            return new IssuePayload
            {
                IssueNumber = IssueNumber,
                Title = Title,
                Body = Body,
                IssueUserLogin = "testuser",
                RepositoryName = "mcp",
                RepositoryOwnerName = "microsoft"
            };
        }
    }
}
