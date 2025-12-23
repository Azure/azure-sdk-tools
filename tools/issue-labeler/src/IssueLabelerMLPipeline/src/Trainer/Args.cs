// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

public struct Args
{
    public string? IssuesDataPath { get; set; }
    public string? CategoryIssuesModelPath { get; set; }
    public string? ServiceIssuesModelPath { get; set; }
    public string[]? SyntheticIssuesCategoryDataPaths { get; set; }
    public string[]? SyntheticIssuesServiceDataPaths { get; set; }
    public string? PullsDataPath { get; set; }
    public string? CategoryPullsModelPath { get; set; }
    public string? ServicePullsModelPath { get; set; }

    static void ShowUsage(string? message)
    {
        // If you provide a path for issue data, you must also provide paths for both category and service issue models, and vice versa.
        // If you provide a path for pull data, you must also provide paths for both category and service pull models, and vice versa.
        // At least one pair of paths (either issue or pull) must be provided.
        Console.WriteLine($$"""
            ERROR: Invalid or missing arguments.{{(message is null ? "" : " " + message)}}

            Required for training the issues models:
              --issues-data                    Path to existing issue data file (TSV file).
              --category-issues-model          Path to category issue prediction model file (ZIP file).
              --service-issues-model           Path to service issue prediction model file (ZIP file).

            Optional for training the issues models:
              --synthetic-issues-category-data Comma-separated list of synthetic issues category data file paths.
              --synthetic-issues-service-data  Comma-separated list of synthetic issues service data file paths.

            Required for training the pull requests models:
              --pulls-data                     Path to existing pull request data file (TSV file).
              --category-pulls-model           Path to category pull request prediction model file (ZIP file).
              --service-pulls-model            Path to service pull request prediction model file (ZIP file).
            """);

        Environment.Exit(1);
    }

    public static Args? Parse(string[] args)
    {
        Queue<string> arguments = new(args);
        ArgUtils argUtils = new(ShowUsage, arguments);
        Args argsData = new();

        while (arguments.Count > 0)
        {
            string argument = arguments.Dequeue();

            switch (argument)
            {
                case "--issues-data":
                    if (!argUtils.TryGetPath("--issues-data", out string? IssuesDataPath))
                    {
                        return null;
                    }
                    argsData.IssuesDataPath = IssuesDataPath;
                    break;

                case "--category-issues-model":
                    if (!argUtils.TryGetPath("--category-issues-model", out string? CategoryIssuesModelPath))
                    {
                        return null;
                    }
                    argsData.CategoryIssuesModelPath = CategoryIssuesModelPath;
                    break;

                case "--service-issues-model":
                    if (!argUtils.TryGetPath("--service-issues-model", out string? ServiceIssuesModelPath))
                    {
                        return null;
                    }
                    argsData.ServiceIssuesModelPath = ServiceIssuesModelPath;
                    break;

                case "--synthetic-issues-category-data":
                    if (!argUtils.TryGetStringArray("--synthetic-issues-category-data", out string[]? syntheticCategoryPaths))
                    {
                        return null;
                    }
                    argsData.SyntheticIssuesCategoryDataPaths = syntheticCategoryPaths;
                    break;

                case "--synthetic-issues-service-data":
                    if (!argUtils.TryGetStringArray("--synthetic-issues-service-data", out string[]? syntheticServicePaths))
                    {
                        return null;
                    }
                    argsData.SyntheticIssuesServiceDataPaths = syntheticServicePaths;
                    break;

                case "--pulls-data":
                    if (!argUtils.TryGetPath("--pulls-data", out string? PullsDataPath))
                    {
                        return null;
                    }
                    argsData.PullsDataPath = PullsDataPath;
                    break;

                case "--category-pulls-model":
                    if (!argUtils.TryGetPath("--category-pulls-model", out string? CategoryPullsModelPath))
                    {
                        return null;
                    }
                    argsData.CategoryPullsModelPath = CategoryPullsModelPath;
                    break;

                case "--service-pulls-model":
                    if (!argUtils.TryGetPath("--service-pulls-model", out string? ServicePullsModelPath))
                    {
                        return null;
                    }
                    argsData.ServicePullsModelPath = ServicePullsModelPath;
                    break;

                default:
                    ShowUsage($"Unrecognized argument: {argument}");
                    return null;
            }
        }

        // Validate that if issues data is provided, both category and service issue models are also provided
        bool hasIssuesData = argsData.IssuesDataPath is not null;
        bool hasCategoryIssuesModel = argsData.CategoryIssuesModelPath is not null;
        bool hasServiceIssuesModel = argsData.ServiceIssuesModelPath is not null;
        bool hasAllIssuesRequirements = hasIssuesData && hasCategoryIssuesModel && hasServiceIssuesModel;
        bool hasAnyIssuesRequirements = hasIssuesData || hasCategoryIssuesModel || hasServiceIssuesModel;

        // Validate that if pulls data is provided, both category and service pull models are also provided
        bool hasPullsData = argsData.PullsDataPath is not null;
        bool hasCategoryPullsModel = argsData.CategoryPullsModelPath is not null;
        bool hasServicePullsModel = argsData.ServicePullsModelPath is not null;
        bool hasAllPullsRequirements = hasPullsData && hasCategoryPullsModel && hasServicePullsModel;
        bool hasAnyPullsRequirements = hasPullsData || hasCategoryPullsModel || hasServicePullsModel;

        if ((hasAnyIssuesRequirements && !hasAllIssuesRequirements) ||
            (hasAnyPullsRequirements && !hasAllPullsRequirements) ||
            (!hasAllIssuesRequirements && !hasAllPullsRequirements))
        {
            ShowUsage(null);
            return null;
        }

        return argsData;
    }
}
