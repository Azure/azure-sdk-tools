namespace IssueLabeler.Shared
{
    public interface IPredictor
    {
        Task<LabelSuggestion> Predict(GitHubIssue issue);
        Task<LabelSuggestion> Predict(GitHubPullRequest issue);
        public string ModelName { get; set; }
    }
}
