using IssueLabeler.Shared.Models;

namespace IssueLabeler.Shared
{
    public interface ILabelRetriever
    {
        string MessageToAddAreaLabelForIssue { get; }
        string MessageToAddAreaLabelForPr { get; }
        bool AddDelayBeforeUpdatingLabels { get; }
        bool AllowTakingLinkedIssueLabel { get; }
        bool CommentWhenMissingAreaLabel { get; }
        bool OkToAddUntriagedLabel { get; }
        bool SkipPrediction { get; }

        string CommentFor(string label);
        HashSet<string> GetNonAreaLabelsForIssueAsync(GitHubIssue issue);
        bool OkToIgnoreThresholdFor(string chosenLabel);
        bool PreferManualLabelingFor(string chosenLabel);
        bool ShouldSkipUpdatingLabels(string issueAuthor);
    }
}