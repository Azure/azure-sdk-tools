using IssueLabeler.Shared.Models;

namespace IssueLabeler.Shared
{
    public class LabelRetriever : ILabelRetriever
    {
        public bool AddDelayBeforeUpdatingLabels { get => _repo.Equals("dotnet-api-docs", StringComparison.OrdinalIgnoreCase); }
        public bool OkToAddUntriagedLabel { get => !_repo.Equals("dotnet-api-docs", StringComparison.OrdinalIgnoreCase); }
        public bool CommentWhenMissingAreaLabel { get => !_repo.Equals("deployment-tools", StringComparison.OrdinalIgnoreCase); }
        public bool SkipPrediction
        {
            get =>
_repo.Equals("deployment-tools", StringComparison.OrdinalIgnoreCase);
        }

        public bool AllowTakingLinkedIssueLabel
        {
            get =>
            (_owner.Equals("dotnet", StringComparison.OrdinalIgnoreCase) && _repo.Equals("runtime", StringComparison.OrdinalIgnoreCase)) ||
            (_owner.Equals("dotnet", StringComparison.OrdinalIgnoreCase) && _repo.Equals("sdk", StringComparison.OrdinalIgnoreCase)) ||
            (_owner.Equals("dotnet", StringComparison.OrdinalIgnoreCase) && _repo.Equals("dotnet-api-docs", StringComparison.OrdinalIgnoreCase));
        }

        public bool PreferManualLabelingFor(string chosenLabel)
        {
            if (_owner.Equals("dotnet", StringComparison.OrdinalIgnoreCase) && _repo.Equals("runtime", StringComparison.OrdinalIgnoreCase))
            {
                return chosenLabel.Equals("area-Infrastructure", StringComparison.OrdinalIgnoreCase) || chosenLabel.Equals("area-System.Runtime", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public bool OkToIgnoreThresholdFor(string chosenLabel)
        {
            if (_owner.Equals("dotnet", StringComparison.OrdinalIgnoreCase) && _repo.Equals("runtime", StringComparison.OrdinalIgnoreCase))
            {
                // return (!chosenLabel.Equals("area-System.Net", StringComparison.OrdinalIgnoreCase) && 
                //     chosenLabel.StartsWith("area-System", StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }

        private readonly string MessageToAddDoc =
               "Note regarding the `new-api-needs-documentation` label:" + Environment.NewLine + Environment.NewLine +
               "This serves as a reminder for when your PR is modifying a ref *.cs file and adding/modifying public APIs, to please make sure the API implementation in the src *.cs file is documented with triple slash comments, so the PR reviewers can sign off that change.";

        private string _areaLabelLinked =>
            _repo.Equals("runtime", StringComparison.OrdinalIgnoreCase) ? "[area label](" +
                @"https://github.com/dotnet/runtime/blob/master/docs/area-owners.md" +
            ")" : "area label";

        public string MessageToAddAreaLabelForPr =>
            "I couldn't figure out the best area label to add to this PR. If you have write-permissions please help me learn by adding exactly one " + _areaLabelLinked + ".";
        public string MessageToAddAreaLabelForIssue =>
            "I couldn't figure out the best area label to add to this issue. If you have write-permissions please help me learn by adding exactly one " + _areaLabelLinked + ".";

        private readonly string _owner;
        private readonly string _repo;
        public LabelRetriever(string owner, string repo)
        {
            _owner = owner;
            _repo = repo;
        }

        public bool ShouldSkipUpdatingLabels(string issueAuthor)
        {
            return _repo.Equals("roslyn", StringComparison.OrdinalIgnoreCase) &&
                _owner.Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
                issueAuthor.Equals("dotnet-bot", StringComparison.OrdinalIgnoreCase);
        }

        public HashSet<string> GetNonAreaLabelsForIssueAsync(GitHubIssue issue)
        {
            var lcs = new HashSet<string>();
            if (issue is GitHubPullRequest pr)
            {
                if (_owner.Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
                    _repo.Equals("runtime", StringComparison.OrdinalIgnoreCase))
                {
                    if (pr.Author.Equals("monojenkins"))
                    {
                        lcs.Add("mono-mirror");
                    }
                }
            }
            else
            {
                if (OkToAddUntriagedLabel)
                {
                    lcs.Add("untriaged");
                }
            }
            return lcs;
        }

        public string CommentFor(string label)
        {
            if (_owner.Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
                _repo.Equals("runtime", StringComparison.OrdinalIgnoreCase) &&
                label.Equals("new-api-needs-documentation", StringComparison.OrdinalIgnoreCase)
                )
            {
                return MessageToAddDoc;
            }
            return default;
        }
    }
}