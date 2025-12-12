// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

public struct Args
{
    public readonly string GitHubToken => Environment.GetEnvironmentVariable("GITHUB_TOKEN")!;
    public string Org { get; set; }
    public List<string> Repos { get; set; }
    public string? IssuesDataPath { get; set; }
    public int? IssuesLimit { get; set; }
    public string? PullsDataPath { get; set; }
    public int? PullsLimit { get; set; }
    public int? PageSize { get; set; }
    public int? PageLimit { get; set; }
    public int[] Retries { get; set; }
    public string[]? ExcludedAuthors { get; set; }
    public bool Verbose { get; set; }

    static void ShowUsage(string? message)
    {
        Console.WriteLine($$"""
            ERROR: Invalid or missing arguments.{{(message is null ? "" : " " + message)}}

            Required environment variables:
              GITHUB_TOKEN            GitHub token to be used for API calls.

            Required arguments:
              --repo                  The GitHub repositories in format org/repo (comma separated for multiple).

            Required for downloading issue data:
              --issues-data           Path for issue data file to create (TSV file).

            Required for downloading pull request data:
              --pulls-data            Path for pull request data file to create (TSV file).

            Optional arguments:
              --issues-limit          Maximum number of issues to download. Defaults to: No limit.
              --pulls-limit           Maximum number of pull requests to download. Defaults to: No limit.
              --page-size             Number of items per page in GitHub API requests.
              --page-limit            Maximum number of pages to retrieve.
              --excluded-authors      Comma-separated list of authors to exclude.
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

                case "--issues-data":
                    if (!argUtils.TryGetPath("--issues-data", out string? IssuesDataPath))
                    {
                        return null;
                    }
                    argsData.IssuesDataPath = IssuesDataPath;
                    break;

                case "--issues-limit":
                    if (!argUtils.TryGetInt("--issues-limit", out int? IssuesLimit))
                    {
                        return null;
                    }
                    argsData.IssuesLimit = IssuesLimit;
                    break;

                case "--pulls-data":
                    if (!argUtils.TryGetPath("--pulls-data", out string? PullsDataPath))
                    {
                        return null;
                    }
                    argsData.PullsDataPath = PullsDataPath;
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

        if (argsData.Org is null || argsData.Repos is null ||
            (argsData.IssuesDataPath is null && argsData.PullsDataPath is null))
        {
            ShowUsage(null);
            return null;
        }

        return argsData;
    }
}
