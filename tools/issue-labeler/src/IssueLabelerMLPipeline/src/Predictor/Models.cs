// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML.Data;

public class Issue
{
    public string? CategoryLabel { get; set; }
    public string? ServiceLabel { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }

    [NoColumn]
    public string[]? Labels { get; set; }
    public string[]? CategoryLabels { get; set; }
    public string[]? ServiceLabels { get; set; }


    [NoColumn]
    public bool HasMoreLabels { get; set; }

    public Issue() { }

    public Issue(GitHubClient.Issue issue)
    {
        Title = issue.Title;
        Description = issue.Body;
        Labels = issue.LabelNames;
        CategoryLabels = issue.CategoryLabelNames;
        ServiceLabels = issue.ServiceLabelNames;
        HasMoreLabels = issue.Labels.HasNextPage;
    }
}

public class PullRequest : Issue
{
    public string? FileNames { get; set; }
    public string? FolderNames { get; set; }

    public PullRequest() { }

    public PullRequest(GitHubClient.PullRequest pull) : base(pull)
    {
        FileNames = string.Join(' ', pull.FileNames);
        FolderNames = string.Join(' ', pull.FolderNames);
    }
}

public class LabelPrediction
{
    public string? PredictedCategoryLabel { get; set; }
    public string? PredictedServiceLabel { get; set; }
    public string? PredictedLabel { get; set; }
    public float[]? Score { get; set; }
}
