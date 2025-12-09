// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

public struct Args
{
    public string? GitHubToken { get; set; }
    public string Org { get; set; }
    public List<string> Repos { get; set; }
    public string? BlobStorageUri { get; set; }
    public string? BlobContainerName { get; set; }
    public string? AppConfigEndpoint { get; set; }
    public string[]? ExcludedAuthors { get; set; }
    public int? IssuesLimit { get; set; }
    public int? PullsLimit { get; set; }
    public int? PageSize { get; set; }
    public int? PageLimit { get; set; }
    public int[] Retries { get; set; }
    public bool Verbose { get; set; }
    public bool DryRun { get; set; }
    public bool RetrainIssues { get; set; }
    public bool RetrainPulls { get; set; }
    public bool TrainWithSyntheticData { get; set; }

    static void ShowUsage(string? message)
    {
        Console.WriteLine($$"""
            ERROR: Invalid or missing arguments.{{(message is null ? "" : " " + message)}}

            Required arguments:
              --github-token               GitHub token to be used for API calls.
              --repo                       The GitHub repositories in format org/repo (comma separated for multiple).
              --blob-storage-uri           Azure Blob Storage URI (e.g., https://account.blob.core.windows.net).
                                           Uses DefaultAzureCredential for authentication.
              --blob-container-name        Azure Blob Storage container name for model storage.
              --app-config-endpoint        Azure App Configuration endpoint URL.
                                           Uses DefaultAzureCredential for authentication.

            Model selection (at least one required):
              --retrain-issues             Retrain issue prediction models.
              --retrain-pulls              Retrain pull request prediction models.

            Optional arguments:
              --excluded-authors           Comma-separated list of authors to exclude.
              --issues-limit               Maximum number of issues to download. Defaults to: No limit.
              --pulls-limit                Maximum number of pull requests to download. Defaults to: No limit.
              --page-size                  Number of items per page in GitHub API requests.
                                           Defaults to: 100 for issues, 25 for pull requests.
              --page-limit                 Maximum number of pages to retrieve.
                                           Defaults to: 1000 for issues, 4000 for pull requests.
              --retries                    Comma-separated retry delays in seconds.
                                           Defaults to: 30,30,300,300,3000,3000.
              --verbose                    Enable verbose output.
              --dry-run                    Download and train models without uploading to blob storage.
              --train-with-synthetic-data  Include synthetic data when training models.
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

        while (arguments.Count > 0)
        {
            string argument = arguments.Dequeue();

            switch (argument)
            {
                case "--github-token":
                    if (!argUtils.TryGetString("--github-token", out string? githubToken))
                    {
                        return null;
                    }
                    argsData.GitHubToken = githubToken;
                    break;

                case "--repo":
                    if (!argUtils.TryGetRepoList("--repo", out string? org, out List<string>? repos))
                    {
                        return null;
                    }
                    argsData.Org = org;
                    argsData.Repos = repos;
                    break;

                case "--blob-storage-uri":
                    if (!argUtils.TryGetString("--blob-storage-uri", out string? blobStorageUri))
                    {
                        return null;
                    }
                    argsData.BlobStorageUri = blobStorageUri;
                    break;

                case "--blob-container-name":
                    if (!argUtils.TryGetString("--blob-container-name", out string? blobContainerName))
                    {
                        return null;
                    }
                    argsData.BlobContainerName = blobContainerName;
                    break;

                case "--app-config-endpoint":
                    if (!argUtils.TryGetString("--app-config-endpoint", out string? appConfigEndpoint))
                    {
                        return null;
                    }
                    argsData.AppConfigEndpoint = appConfigEndpoint;
                    break;

                case "--retrain-issues":
                    argsData.RetrainIssues = true;
                    break;

                case "--retrain-pulls":
                    argsData.RetrainPulls = true;
                    break;

                case "--excluded-authors":
                    if (!argUtils.TryGetStringArray("--excluded-authors", out string[]? excludedAuthors))
                    {
                        return null;
                    }
                    argsData.ExcludedAuthors = excludedAuthors;
                    break;

                case "--issues-limit":
                    if (!argUtils.TryGetInt("--issues-limit", out int? issuesLimit))
                    {
                        return null;
                    }
                    argsData.IssuesLimit = issuesLimit;
                    break;

                case "--pulls-limit":
                    if (!argUtils.TryGetInt("--pulls-limit", out int? pullsLimit))
                    {
                        return null;
                    }
                    argsData.PullsLimit = pullsLimit;
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

                case "--dry-run":
                    argsData.DryRun = true;
                    break;

                case "--train-with-synthetic-data":
                    argsData.TrainWithSyntheticData = true;
                    break;

                default:
                    ShowUsage($"Unrecognized argument: {argument}");
                    return null;
            }
        }

        // Validate required arguments
        if (string.IsNullOrEmpty(argsData.GitHubToken))
        {
            ShowUsage("Missing required --github-token argument.");
            return null;
        }

        if (argsData.Org is null || argsData.Repos is null || argsData.Repos.Count == 0)
        {
            ShowUsage("Missing required --repo argument.");
            return null;
        }

        if (!argsData.DryRun && argsData.BlobStorageUri is null)
        {
            ShowUsage("Missing required --blob-storage-uri argument (required when not in dry-run mode).");
            return null;
        }

        if (!argsData.DryRun && argsData.BlobContainerName is null)
        {
            ShowUsage("Missing required --blob-container-name argument (required when not in dry-run mode).");
            return null;
        }

        if (argsData.AppConfigEndpoint is null)
        {
            ShowUsage("Missing required --app-config-endpoint argument.");
            return null;
        }

        if (!argsData.RetrainIssues && !argsData.RetrainPulls)
        {
            ShowUsage("At least one of --retrain-issues or --retrain-pulls must be specified.");
            return null;
        }

        return argsData;
    }
}
