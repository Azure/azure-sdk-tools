// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.GitHub;

namespace Azure.Sdk.Tools.Mock.Handlers.GitHub;

/// <summary>Mock handler for azsdk_get_github_user_details.</summary>
public class GetGitHubUserDetailsHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_github_user_details";
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var login = arguments?.GetValueOrDefault("userName")?.ToString() ?? "contoso-user";
        return new DefaultCommandResponse
        {
            Message = $"Retrieved user details for {login}",
            Result = new
            {
                login,
                name = "Contoso User",
                email = $"{login}@microsoft.com",
                company = "Microsoft"
            }
        };
    }
}

internal static class GitHubMockResponses
{
    public static PullRequestDetails ContosoPr(string? url = null) => new()
    {
        pullRequestNumber = 45001,
        Author = "contoso-user",
        Status = "open",
        Url = url ?? "https://github.com/Azure/azure-sdk-for-net/pull/45001",
        IsMerged = false,
        IsMergeable = true,
        Checks = ["ci: passed", "live-tests: pending"],
        Comments = []
    };
}

/// <summary>Mock handler for azsdk_get_pull_request_link_for_current_branch.</summary>
public class GetPrLinkForCurrentBranchHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_pull_request_link_for_current_branch";
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var url = "https://github.com/Azure/azure-sdk-for-net/pull/45001";
        return new GetPullRequestResponse
        {
            PullRequestUrl = url,
            PullRequest = GitHubMockResponses.ContosoPr(url)
        };
    }
}

/// <summary>Mock handler for azsdk_create_pull_request.</summary>
public class CreatePullRequestHandler : IMockToolHandler
{
    public string ToolName => "azsdk_create_pull_request";
    public CommandResponse Handle(Dictionary<string, object?>? arguments) => new CreatePullRequestResponse
    {
        PullRequestUrl = "https://github.com/Azure/azure-sdk-for-net/pull/45002",
        Messages = ["Pull request created (mock)"]
    };
}

/// <summary>Mock handler for azsdk_get_pull_request.</summary>
public class GetPullRequestHandler : IMockToolHandler
{
    public string ToolName => "azsdk_get_pull_request";
    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var url = arguments?.GetValueOrDefault("pullRequestUrl")?.ToString()
            ?? "https://github.com/Azure/azure-sdk-for-net/pull/45001";
        return new GetPullRequestResponse
        {
            PullRequestUrl = url,
            PullRequest = GitHubMockResponses.ContosoPr(url)
        };
    }
}
