// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

public struct Args
{
    public string GitHubToken => Environment.GetEnvironmentVariable("GITHUB_TOKEN")!;
    public string Org { get; set; }
    public string Repo { get; set; }
    public float Threshold { get; set; }
    public string[]? ExcludedAuthors { get; set; }
    public string? CategoryIssuesModelPath { get; set; }
    public string? ServiceIssuesModelPath { get; set; }
    public List<ulong>? Issues { get; set; }
    public string? CategoryPullsModelPath { get; set; }
    public string? ServicePullsModelPath { get; set; }
    public List<ulong>? Pulls { get; set; }
    public string? DefaultLabel { get; set; }
    public int[] Retries { get; set; }
    public bool Verbose { get; set; }

    static void ShowUsage(string? message)
    {
        Console.WriteLine($$"""
            ERROR: Invalid or missing arguments.{{(message is null ? "" : " " + message)}}

            Required environment variables:
              GITHUB_TOKEN                GitHub token to be used for API calls.

            Required arguments:
              --repo                      GitHub repository in the format {org}/{repo}.

            Required for predicting issue labels:
              --category-issues-model     Path to the category issues prediction model file (ZIP file).
              --service-issues-model      Path to the service issues prediction model file (ZIP file).
              --issues                    Comma-separated list of issue number ranges.
                                          Example: 1-3,7,5-9.

            Required for predicting pull request labels:
              --category-pulls-model      Path to the category pulls prediction model file (ZIP file).
              --service-pulls-model       Path to the service pulls prediction model file (ZIP file).
              --pulls                     Comma-separated list of pull request number ranges.
                                          Example: 1-3,7,5-9.

            Optional arguments:
              --threshold                 Minimum prediction confidence threshold. Range (0,1].
                                          Defaults to: 0.4.
              --excluded-authors          Comma-separated list of authors to exclude.
              --retries                   Comma-separated retry delays in seconds.
                                          Defaults to: 30,30,300,300,3000,3000.
              --verbose                   Enable verbose output.
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
                    if (!argUtils.TryGetRepo("--repo", out string? org, out string? repo))
                    {
                        return null;
                    }
                    argsData.Org = org;
                    argsData.Repo = repo;
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

                case "--issues":
                    if (!argUtils.TryGetNumberRanges("--issues", out List<ulong>? issues))
                    {
                        return null;
                    }
                    argsData.Issues = issues;
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

                case "--pulls":
                    if (!argUtils.TryGetNumberRanges("--pulls", out List<ulong>? pulls))
                    {
                        return null;
                    }
                    argsData.Pulls = pulls;
                    break;

                case "--threshold":
                    if (!argUtils.TryGetFloat("--threshold", out float? threshold))
                    {
                        return null;
                    }
                    argsData.Threshold = threshold.Value;
                    break;

                case "--excluded-authors":
                    if (!argUtils.TryGetStringArray("--excluded-authors", out string[]? excludedAuthors))
                    {
                        return null;
                    }
                    argsData.ExcludedAuthors = excludedAuthors;
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

        if (argsData.Org is null || argsData.Repo is null ||
            (argsData.Issues is null && argsData.Pulls is null) ||
            (argsData.Issues is not null && argsData.CategoryIssuesModelPath is null && argsData.ServiceIssuesModelPath is null) ||
            (argsData.Pulls is not null && argsData.CategoryPullsModelPath is null && argsData.ServicePullsModelPath is null))
        {
            return null;
        }

        return argsData;
    }
}
