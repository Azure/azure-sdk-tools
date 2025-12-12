// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

public struct Args
{
    public readonly string GitHubToken => Environment.GetEnvironmentVariable("GITHUB_TOKEN")!;
    public string Org { get; set; }
    public List<string> Repos { get; set; }
    public float Threshold { get; set; }
    public string[]? ExcludedAuthors { get; set; }
    public string? CategoryIssuesModelPath { get; set; }
    public string? ServiceIssuesModelPath { get; set; }
    public int? IssuesLimit { get; set; }
    public string? CategoryPullsModelPath { get; set; }
    public string? ServicePullsModelPath { get; set; }
    public int? PullsLimit { get; set; }
    public int? PageSize { get; set; }
    public int? PageLimit { get; set; }
    public int[] Retries { get; set; }
    public bool Verbose { get; set; }

    static void ShowUsage(string? message)
    {
        Console.WriteLine($$"""
            ERROR: Invalid or missing arguments.{{(message is null ? "" : " " + message)}}

            Required environment variables:
              GITHUB_TOKEN            GitHub token to be used for API calls.

            Required arguments:
              --repo                  The GitHub repositories in format org/repo (comma separated for multiple).

            Required for testing the issues models:
              --category-issues-model Path to existing category issues prediction model file (ZIP file).
              --service-issues-model  Path to existing service issues prediction model file (ZIP file).

            Required for testing the pull requests models:
              --category-pulls-model  Path to existing category pulls prediction model file (ZIP file).
              --service-pulls-model   Path to existing service pulls prediction model file (ZIP file).

            Optional arguments:
              --excluded-authors      Comma-separated list of authors to exclude.
              --threshold             Minimum prediction confidence threshold. Range (0,1].
                                      Defaults to: 0.4.
              --issues-limit          Maximum number of issues to download. Defaults to: No limit.
              --pulls-limit           Maximum number of pull requests to download. Defaults to: No limit.
              --page-size             Number of items per page in GitHub API requests.
                                      Defaults to: 100 for issues, 25 for pull requests.
              --page-limit            Maximum number of pages to retrieve.
                                      Defaults to: 1000 for issues, 4000 for pull requests.
              --retries               Comma-separated retry delays in seconds.
                                      Defaults to: 30,30,300,300,3000,3000.
              --verbose               Enable verbose output.
            """);

        Environment.Exit(1);
    }

    public static Args? Parse(string[] args)
    {
        Queue<string> arguments = new(args);
        ArgUtils argUtils = new(ShowUsage, arguments);

        Args argsData = new()
        {
            Threshold = 0.4f,
            Retries = [30, 30, 300, 300, 3000, 3000]
        };

        if (string.IsNullOrEmpty(argsData.GitHubToken))
        {
            ShowUsage("Environment variable GITHUB_TOKEN is empty.");
            return null;
        }

        while (arguments.Count > 0)
        {
            string argument = arguments.Dequeue();

            switch (argument)
            {
                case "--repo":
                    if (!argUtils.TryGetRepoList("--repo", out string? org, out List<string>? repos))
                    {
                        return null;
                    }
                    argsData.Org = org;
                    argsData.Repos = repos;
                    break;

                case "--excluded-authors":
                    if (!argUtils.TryGetStringArray("--excluded-authors", out string[]? excludedAuthors))
                    {
                        return null;
                    }
                    argsData.ExcludedAuthors = excludedAuthors;
                    break;

                case "--threshold":
                    if (!argUtils.TryGetFloat("--threshold", out float? threshold))
                    {
                        return null;
                    }
                    argsData.Threshold = threshold.Value;
                    break;



                case "--category-issues-model":
                    if (!argUtils.TryGetPath("--category-issues-model", out string? categoryIssuesModelPath))
                    {
                        return null;
                    }
                    argsData.CategoryIssuesModelPath = categoryIssuesModelPath;
                    break;

                case "--service-issues-model":
                    if (!argUtils.TryGetPath("--service-issues-model", out string? serviceIssuesModelPath))
                    {
                        return null;
                    }
                    argsData.ServiceIssuesModelPath = serviceIssuesModelPath;
                    break;

                case "--issues-limit":
                    if (!argUtils.TryGetInt("--issues-limit", out int? IssuesLimit))
                    {
                        return null;
                    }
                    argsData.IssuesLimit = IssuesLimit;
                    break;

                case "--category-pulls-model":
                    if (!argUtils.TryGetPath("--category-pulls-model", out string? categoryPullsModelPath))
                    {
                        return null;
                    }
                    argsData.CategoryPullsModelPath = categoryPullsModelPath;
                    break;

                case "--service-pulls-model":
                    if (!argUtils.TryGetPath("--service-pulls-model", out string? servicePullsModelPath))
                    {
                        return null;
                    }
                    argsData.ServicePullsModelPath = servicePullsModelPath;
                    break;

                case "--pulls-limit":
                    if (!argUtils.TryGetInt("--pulls-limit", out int? PullsLimit))
                    {
                        return null;
                    }
                    argsData.PullsLimit = PullsLimit;
                    break;

                case "--page-size":
                    if (!argUtils.TryGetInt("--page-size", out int? pageSize))
                    {
                        return null;
                    }
                    argsData.PageSize = pageSize;
                    break;

                case "--page-limit":
                    if (!argUtils.TryGetInt("--page-limit", out int? pageLimit))
                    {
                        return null;
                    }
                    argsData.PageLimit = pageLimit;
                    break;

                case "--retries":
                    if (!argUtils.TryGetIntArray("--retries", out int[]? retries))
                    {
                        return null;
                    }
                    argsData.Retries = retries;
                    break;

                case "--verbose":
                    argsData.Verbose = true;
                    break;

                default:
                    ShowUsage($"Unrecognized argument: {argument}");
                    return null;
            }
        }

        if (argsData.Org is null || argsData.Repos.Count == 0 ||
            ((argsData.CategoryIssuesModelPath is null || argsData.ServiceIssuesModelPath is null) &&
             (argsData.CategoryPullsModelPath is null || argsData.ServicePullsModelPath is null)))
        {
            ShowUsage(null);
            return null;
        }

        return argsData;
    }
}
