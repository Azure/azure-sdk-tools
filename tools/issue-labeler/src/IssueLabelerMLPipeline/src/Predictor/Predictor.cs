// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using GitHubClient;
using Microsoft.ML;
using Microsoft.ML.Data;

// Console application entry point (top-level statements)
if (Args.Parse(args) is not Args argsData) return 1;

List<Task<(ulong Number, string ResultMessage, bool Success)>> tasks = new();

// (This console application implementation will need to be completed with the original logic)
{
    // Load prediction engines for issues
    PredictionEngine<Issue, LabelPrediction>? categoryIssuePredictor = null;
    PredictionEngine<Issue, LabelPrediction>? serviceIssuePredictor = null;

    Console.WriteLine($"Loading prediction engine for category issues model...");
    var categoryIssueContext = new MLContext();
    var categoryIssueModel = categoryIssueContext.Model.Load(argsData.CategoryIssuesModelPath, out _);
    categoryIssuePredictor = categoryIssueContext.Model.CreatePredictionEngine<Issue, LabelPrediction>(categoryIssueModel);
    Console.WriteLine($"Category issues prediction engine ready.");

    Console.WriteLine($"Loading prediction engine for service issues model...");
    var serviceIssueContext = new MLContext();
    var serviceIssueModel = serviceIssueContext.Model.Load(argsData.ServiceIssuesModelPath, out _);
    serviceIssuePredictor = serviceIssueContext.Model.CreatePredictionEngine<Issue, LabelPrediction>(serviceIssueModel);
    Console.WriteLine($"Service issues prediction engine ready.");

    foreach (ulong issueNumber in argsData.Issues)
    {
        var result = await GitHubApi.GetIssue(argsData.GitHubToken, argsData.Org, argsData.Repo, issueNumber, argsData.Retries, argsData.Verbose);

        if (result is null)
        {
            Console.WriteLine($"[Issue {argsData.Org}/{argsData.Repo}#{issueNumber}] could not be found or downloaded. Skipped.");
            continue;
        }

        if (argsData.ExcludedAuthors is not null && result.Author?.Login is not null && argsData.ExcludedAuthors.Contains(result.Author.Login, StringComparer.InvariantCultureIgnoreCase))
        {
            Console.WriteLine($"[Issue {argsData.Org}/{argsData.Repo}#{issueNumber}] Author '{result.Author.Login}' is in excluded list. Skipped.");
            continue;
        }

        tasks.Add(Task.Run(() => ProcessCombinedPrediction(
            categoryIssuePredictor,
            serviceIssuePredictor,
            issueNumber,
            new Issue(result),
            ModelType.Issue,
            argsData.Threshold
        )));

        Console.WriteLine($"[Issue {argsData.Org}/{argsData.Repo}#{issueNumber}] Queued for combined prediction.");
    }
}

// Process Pull Requests (both category and service models together)
if (argsData.Pulls is not null && 
    argsData.CategoryPullsModelPath is not null && argsData.ServicePullsModelPath is not null)
{
    // Load prediction engines for pull requests
    PredictionEngine<PullRequest, LabelPrediction>? categoryPullPredictor = null;
    PredictionEngine<PullRequest, LabelPrediction>? servicePullPredictor = null;

    Console.WriteLine($"Loading prediction engine for category pulls model...");
    var categoryPullContext = new MLContext();
    var categoryPullModel = categoryPullContext.Model.Load(argsData.CategoryPullsModelPath, out _);
    categoryPullPredictor = categoryPullContext.Model.CreatePredictionEngine<PullRequest, LabelPrediction>(categoryPullModel);
    Console.WriteLine($"Category pulls prediction engine ready.");

    Console.WriteLine($"Loading prediction engine for service pulls model...");
    var servicePullContext = new MLContext();
    var servicePullModel = servicePullContext.Model.Load(argsData.ServicePullsModelPath, out _);
    servicePullPredictor = servicePullContext.Model.CreatePredictionEngine<PullRequest, LabelPrediction>(servicePullModel);
    Console.WriteLine($"Service pulls prediction engine ready.");

    foreach (ulong pullNumber in argsData.Pulls)
    {
        var result = await GitHubApi.GetPullRequest(argsData.GitHubToken, argsData.Org, argsData.Repo, pullNumber, argsData.Retries, argsData.Verbose);

        if (result is null)
        {
            Console.WriteLine($"[Pull Request {argsData.Org}/{argsData.Repo}#{pullNumber}] could not be found or downloaded. Skipped.");
            continue;
        }

        if (argsData.ExcludedAuthors is not null && result.Author?.Login is not null && argsData.ExcludedAuthors.Contains(result.Author.Login))
        {
            Console.WriteLine($"[Pull Request {argsData.Org}/{argsData.Repo}#{pullNumber}] Author '{result.Author.Login}' is in excluded list. Skipped.");
            continue;
        }

        tasks.Add(Task.Run(() => ProcessCombinedPrediction(
            categoryPullPredictor,
            servicePullPredictor,
            pullNumber,
            new PullRequest(result),
            ModelType.PullRequest,
            argsData.Threshold
        )));

        Console.WriteLine($"[Pull Request {argsData.Org}/{argsData.Repo}#{pullNumber}] Queued for combined prediction.");
    }
}

var (predictionResults, success) = await App.RunTasks(tasks);

foreach (var prediction in predictionResults.OrderBy(p => p.Number))
{
    Console.WriteLine(prediction.ResultMessage);
}

return success ? 0 : 1;

(ulong Number, string ResultMessage, bool Success) ProcessCombinedPrediction<T>(
    PredictionEngine<T, LabelPrediction>? categoryPredictor, 
    PredictionEngine<T, LabelPrediction>? servicePredictor, 
    ulong number, 
    T issueOrPull, 
    ModelType type, 
    float threshold) where T : Issue
{
    string typeName = type == ModelType.PullRequest ? "Pull Request" : "Issue";
    List<string> resultMessageParts = [];

    Console.WriteLine($"- {argsData.Org}/{argsData.Repo}#{number}");

    // Run predictions for both models
    if (categoryPredictor is not null)
    {
        var categoryPrediction = categoryPredictor.Predict(issueOrPull);
        if (categoryPrediction.Score is not null && categoryPrediction.Score.Length > 0)
        {
            VBuffer<ReadOnlyMemory<char>> categoryLabels = default;
            categoryPredictor.OutputSchema[nameof(LabelPrediction.Score)].GetSlotNames(ref categoryLabels);

            var bestCategory = categoryPrediction.Score
                .Select((score, index) => new { Score = score, Label = categoryLabels.GetItemOrDefault(index).ToString() })
                .OrderByDescending(p => p.Score)
                .FirstOrDefault(p => p.Score >= threshold);

            if (bestCategory is not null)
            {
                resultMessageParts.Add($"Category: {bestCategory.Label} (Score: {bestCategory.Score})");
            }
            else
            {
                resultMessageParts.Add($"    - No category label prediction met the threshold of {threshold}.");
            }
        }
        else
        {
            resultMessageParts.Add($"    - No category prediction was made.");
        }
    }

    if (servicePredictor is not null)
    {
        var servicePrediction = servicePredictor.Predict(issueOrPull);
        if (servicePrediction.Score is not null && servicePrediction.Score.Length > 0)
        {
            VBuffer<ReadOnlyMemory<char>> serviceLabels = default;
            servicePredictor.OutputSchema[nameof(LabelPrediction.Score)].GetSlotNames(ref serviceLabels);

            var bestService = servicePrediction.Score
                .Select((score, index) => new { Score = score, Label = serviceLabels.GetItemOrDefault(index).ToString() })
                .OrderByDescending(p => p.Score)
                .FirstOrDefault(p => p.Score >= threshold);

            if (bestService is not null)
            {
                resultMessageParts.Add($"Service: {bestService.Label} (Score: {bestService.Score})");
            }
            else
            {
                resultMessageParts.Add($"    - No service label prediction met the threshold of {threshold}.");
            }
        }
        else
        {
            resultMessageParts.Add($"    - No service prediction was made.");
        }
    }

    string resultMessage = resultMessageParts.Count > 0 
        ? $"[{typeName} #{number}] {string.Join(", ", resultMessageParts)}"
        : $"[{typeName} #{number}] No predictions met the threshold.";
    
    return (number, resultMessage, true);
}
