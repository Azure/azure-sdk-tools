// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

if (Args.Parse(args) is not Args argsData) return 1;

List<Task> tasks = [];

if (!string.IsNullOrEmpty(argsData.IssuesDataPath))
{
    tasks.Add(GitHubDataDownloader.DownloadIssuesAsync(
        argsData.GitHubToken,
        argsData.Org,
        argsData.Repos,
        argsData.IssuesDataPath,
        argsData.IssuesLimit,
        argsData.PageSize,
        argsData.PageLimit,
        argsData.Retries,
        argsData.ExcludedAuthors,
        argsData.Verbose));
}

if (!string.IsNullOrEmpty(argsData.PullsDataPath))
{
    tasks.Add(GitHubDataDownloader.DownloadPullRequestsAsync(
        argsData.GitHubToken,
        argsData.Org,
        argsData.Repos,
        argsData.PullsDataPath,
        argsData.PullsLimit,
        argsData.PageSize,
        argsData.PageLimit,
        argsData.Retries,
        argsData.ExcludedAuthors,
        argsData.Verbose));
}

var success = await App.RunTasks(tasks);
return success ? 0 : 1;
