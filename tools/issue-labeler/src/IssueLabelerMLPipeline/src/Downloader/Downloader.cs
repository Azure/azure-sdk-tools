// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Actions.Core.Extensions;
using Actions.Core.Services;
using GitHubClient;
using Microsoft.Extensions.DependencyInjection;
using static DataFileUtils;

using var provider = new ServiceCollection()
    .AddGitHubActionsCore()
    .BuildServiceProvider();

var action = provider.GetRequiredService<ICoreService>();
if (Args.Parse(args, action) is not Args argsData) return 1;

List<Task> tasks = [];

if (!string.IsNullOrEmpty(argsData.IssuesDataPath))
{
    EnsureOutputDirectory(argsData.IssuesDataPath);
    tasks.Add(Task.Run(() => DownloadIssues(argsData.IssuesDataPath)));
}

if (!string.IsNullOrEmpty(argsData.PullsDataPath))
{
    EnsureOutputDirectory(argsData.PullsDataPath);
    tasks.Add(Task.Run(() => DownloadPullRequests(argsData.PullsDataPath)));
}

var success = await App.RunTasks(tasks, action);
return success ? 0 : 1;

async Task DownloadIssues(string outputPath, bool writeToBlobStorage = false)
{
    action.WriteInfo($"Issues Data Path: {outputPath}");

    byte perFlushCount = 0;

    using StreamWriter writer = new StreamWriter(outputPath);
    writer.WriteLine(FormatIssueRecord("CategoryLabel", "ServiceLabel", "Title", "Body"));

    foreach (var repo in argsData.Repos)
    {
        await foreach (var result in GitHubApi.DownloadIssues(argsData.GitHubToken, argsData.Org, repo,
                                                              argsData.IssuesLimit, argsData.PageSize, argsData.PageLimit,
                                                              argsData.Retries, argsData.ExcludedAuthors, action, argsData.Verbose))
        {
            writer.WriteLine(FormatIssueRecord(result.CategoryLabel, result.ServiceLabel, result.Issue.Title, result.Issue.Body));

            if (++perFlushCount == 100)
            {
                writer.Flush();
                perFlushCount = 0;
            }
        }
    }

    writer.Close();
}

async Task DownloadPullRequests(string outputPath)
{
    action.WriteInfo($"Pulls Data Path: {outputPath}");

    byte perFlushCount = 0;

    using StreamWriter writer = new StreamWriter(outputPath);
    writer.WriteLine(FormatPullRequestRecord("CategoryLabel", "ServiceLabel", "Title", "Body", ["FileNames"], ["FolderNames"]));

    foreach (var repo in argsData.Repos)
    {
        await foreach (var result in GitHubApi.DownloadPullRequests(argsData.GitHubToken, argsData.Org, repo,
                                                                    argsData.PullsLimit, argsData.PageSize, argsData.PageLimit,
                                                                    argsData.Retries, argsData.ExcludedAuthors, action, argsData.Verbose))
        {
            writer.WriteLine(FormatPullRequestRecord(result.CategoryLabel, result.ServiceLabel, result.PullRequest.Title, result.PullRequest.Body, result.PullRequest.FileNames, result.PullRequest.FolderNames));

            if (++perFlushCount == 100)
            {
                writer.Flush();
                perFlushCount = 0;
            }
        }
    }

    writer.Close();
}
