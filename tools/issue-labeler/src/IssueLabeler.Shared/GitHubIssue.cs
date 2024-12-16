// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable 649 // We don't care about unsused fields here, because they are mapped with the input file.

using Microsoft.ML.Data;
using Octokit;

namespace IssueLabeler.Shared
{

    public class RepoIssueResult
    {
        public string Repo { get; set; }
        public string Owner { get; set; }
        public IReadOnlyList<Issue> Issues { get; set; }
        public int TotalCount { get; set; }
        public List<Label> LabelsOfInterest { get; set; }
    }

    public sealed class RemoteLabelPrediction
    {
        // Meant to deserialize a JSON response like this:
        //{
        //    "labelScores":
        //    [
        //        {
        //            "labelName": "area-infrastructure",
        //            "score": 0.988357544
        //        },
        //        {
        //            "labelName": "area-mvc",
        //            "score": 0.008182112
        //        },
        //        {
        //            "labelName": "area-servers",
        //            "score": 0.002301987
        //        }
        //    ]
        //}
        public List<RemoteLabelPredictionScore> LabelScores { get; set; }

    }

    public sealed class RemoteLabelPredictionScore
    {
        public string LabelName { get; set; }
        public float Score { get; set; }
    }
    public class LabelSuggestionViewModel
    {
        public Issue Issue { get; set; }
        public List<LabelScore> LabelScores { get; set; }
        public string RepoOwner { get; set; }
        public string RepoName { get; set; }

        public string ErrorMessage { get; set; }
    }
    public class LabelScore
    {
        public ScoredLabel ScoredLabel { get; set; }
        public Label Label { get; set; }
    }
    public class GitHubPullRequest : GitHubIssue
    {
        [LoadColumn(9)]
        public float FileCount;

        [LoadColumn(10)]
        public string Files;

        [LoadColumn(11)]
        public string Filenames;

        [LoadColumn(12)]
        public string FileExtensions;

        [LoadColumn(13)]
        public string FolderNames;

        [LoadColumn(14)]
        public string Folders;
    }

    public class GitHubIssue
    {
        [LoadColumn(0)]
        public string CombinedID;

        [LoadColumn(1)]
        public float ID;

        [LoadColumn(2)]
        public string Label;

        [LoadColumn(3)]
        public string Title;

        [LoadColumn(4)]
        public string Description;

        [LoadColumn(5)]
        public string Author;

        [LoadColumn(6)]
        public float IsPR;

        [LoadColumn(7)]
        public string UserMentions;

        [LoadColumn(8)]
        public float NumMentions;
    }
}
