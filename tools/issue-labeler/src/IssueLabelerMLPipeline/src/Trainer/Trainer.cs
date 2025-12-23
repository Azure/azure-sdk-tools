// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var config = Args.Parse(args);
if (config is not Args argsData) return 1;

var success = true;

if (argsData.IssuesDataPath is not null && 
    argsData.CategoryIssuesModelPath is not null && 
    argsData.ServiceIssuesModelPath is not null)
{
    try
    {
        ModelTrainer.CreateModel(argsData.IssuesDataPath, argsData.CategoryIssuesModelPath, ModelType.Issue, LabelType.Category, argsData.SyntheticIssuesCategoryDataPaths);
        ModelTrainer.CreateModel(argsData.IssuesDataPath, argsData.ServiceIssuesModelPath, ModelType.Issue, LabelType.Service, argsData.SyntheticIssuesServiceDataPaths);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error training issues models: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        success = false;
    }
}

if (argsData.PullsDataPath is not null && 
    argsData.CategoryPullsModelPath is not null && 
    argsData.ServicePullsModelPath is not null)
{
    try
    {
        ModelTrainer.CreateModel(argsData.PullsDataPath, argsData.CategoryPullsModelPath, ModelType.PullRequest, LabelType.Category, argsData.SyntheticIssuesCategoryDataPaths);
        ModelTrainer.CreateModel(argsData.PullsDataPath, argsData.ServicePullsModelPath, ModelType.PullRequest, LabelType.Service, argsData.SyntheticIssuesServiceDataPaths);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error training pull request models: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        success = false;
    }
}

return success ? 0 : 1;
