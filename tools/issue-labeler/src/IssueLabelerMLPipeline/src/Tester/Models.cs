// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

public class Issue
{
    public string Repo { get; set; }
    public ulong Number { get; set; }
    public string? CategoryLabel { get; set; }
    public string? ServiceLabel { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }

    public Issue(string repo, GitHubClient.Issue issue)
    {
        Repo = repo;
        Number = issue.Number;
        Title = issue.Title;
        Description = issue.Body;
    }

    public Issue(string repo, GitHubClient.Issue issue, string? categoryLabel, string? serviceLabel) : this(repo, issue)
    {
        CategoryLabel = categoryLabel;
        ServiceLabel = serviceLabel;
    }
}

public class PullRequest : Issue
{
    public string? FileNames { get; set; }
    public string? FolderNames { get; set; }

    public PullRequest(string repo, GitHubClient.PullRequest pull) : base(repo, pull)
    {
        FileNames = string.Join(' ', pull.FileNames);
        FolderNames = string.Join(' ', pull.FolderNames);
    }

    public PullRequest(string repo, GitHubClient.PullRequest pull, string? categoryLabel, string? serviceLabel) : base(repo, pull, categoryLabel, serviceLabel)
    {
        FileNames = string.Join(' ', pull.FileNames);
        FolderNames = string.Join(' ', pull.FolderNames);
    }
}

public class LabelPrediction
{
    public string? PredictedLabel { get; set; }
    public float[]? Score { get; set; }
}
