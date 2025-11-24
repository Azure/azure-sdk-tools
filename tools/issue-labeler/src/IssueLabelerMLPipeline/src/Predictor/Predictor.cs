// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Actions.Core.Extensions;
using Actions.Core.Services;
using Actions.Core.Summaries;
using GitHubClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;
using Microsoft.ML.Data;

using var provider = new ServiceCollection()
    .AddGitHubActionsCore()
    .BuildServiceProvider();

var action = provider.GetRequiredService<ICoreService>();
if (Args.Parse(args, action) is not Args argsData) return 1;

List<Task<(ulong Number, string ResultMessage, bool Success)>> tasks = new();

// Process Issues (both category and service models together)
if (argsData.Issues is not null && 
    argsData.CategoryIssuesModelPath is not null && argsData.ServiceIssuesModelPath is not null)
{
    // Load prediction engines for issues
    PredictionEngine<Issue, LabelPrediction>? categoryIssuePredictor = null;
    PredictionEngine<Issue, LabelPrediction>? serviceIssuePredictor = null;

    await action.WriteStatusAsync($"Loading prediction engine for category issues model...");
    var categoryIssueContext = new MLContext();
    var categoryIssueModel = categoryIssueContext.Model.Load(argsData.CategoryIssuesModelPath, out _);
    categoryIssuePredictor = categoryIssueContext.Model.CreatePredictionEngine<Issue, LabelPrediction>(categoryIssueModel);
    await action.WriteStatusAsync($"Category issues prediction engine ready.");

    await action.WriteStatusAsync($"Loading prediction engine for service issues model...");
    var serviceIssueContext = new MLContext();
    var serviceIssueModel = serviceIssueContext.Model.Load(argsData.ServiceIssuesModelPath, out _);
    serviceIssuePredictor = serviceIssueContext.Model.CreatePredictionEngine<Issue, LabelPrediction>(serviceIssueModel);
    await action.WriteStatusAsync($"Service issues prediction engine ready.");

    foreach (ulong issueNumber in argsData.Issues)
    {
        var result = await GitHubApi.GetIssue(argsData.GitHubToken, argsData.Org, argsData.Repo, issueNumber, argsData.Retries, action, argsData.Verbose);

        if (result is null)
        {
            action.WriteNotice($"[Issue {argsData.Org}/{argsData.Repo}#{issueNumber}] could not be found or downloaded. Skipped.");
            continue;
        }

        if (argsData.ExcludedAuthors is not null && result.Author?.Login is not null && argsData.ExcludedAuthors.Contains(result.Author.Login, StringComparer.InvariantCultureIgnoreCase))
        {
            action.WriteNotice($"[Issue {argsData.Org}/{argsData.Repo}#{issueNumber}] Author '{result.Author.Login}' is in excluded list. Skipped.");
            continue;
        }

        tasks.Add(Task.Run(() => ProcessCombinedPrediction(
            categoryIssuePredictor,
            serviceIssuePredictor,
            issueNumber,
            new Issue(result),
            argsData.DefaultLabel,
            ModelType.Issue,
            argsData.Retries,
            argsData.Test,
            argsData.Threshold
        )));

        action.WriteInfo($"[Issue {argsData.Org}/{argsData.Repo}#{issueNumber}] Queued for combined prediction.");
    }
}

// Process Pull Requests (both category and service models together)
if (argsData.Pulls is not null && 
    argsData.CategoryPullsModelPath is not null && argsData.ServicePullsModelPath is not null)
{
    // Load prediction engines for pull requests
    PredictionEngine<PullRequest, LabelPrediction>? categoryPullPredictor = null;
    PredictionEngine<PullRequest, LabelPrediction>? servicePullPredictor = null;

    await action.WriteStatusAsync($"Loading prediction engine for category pulls model...");
    var categoryPullContext = new MLContext();
    var categoryPullModel = categoryPullContext.Model.Load(argsData.CategoryPullsModelPath, out _);
    categoryPullPredictor = categoryPullContext.Model.CreatePredictionEngine<PullRequest, LabelPrediction>(categoryPullModel);
    await action.WriteStatusAsync($"Category pulls prediction engine ready.");

    await action.WriteStatusAsync($"Loading prediction engine for service pulls model...");
    var servicePullContext = new MLContext();
    var servicePullModel = servicePullContext.Model.Load(argsData.ServicePullsModelPath, out _);
    servicePullPredictor = servicePullContext.Model.CreatePredictionEngine<PullRequest, LabelPrediction>(servicePullModel);
    await action.WriteStatusAsync($"Service pulls prediction engine ready.");

    foreach (ulong pullNumber in argsData.Pulls)
    {
        var result = await GitHubApi.GetPullRequest(argsData.GitHubToken, argsData.Org, argsData.Repo, pullNumber, argsData.Retries, action, argsData.Verbose);

        if (result is null)
        {
            action.WriteNotice($"[Pull Request {argsData.Org}/{argsData.Repo}#{pullNumber}] could not be found or downloaded. Skipped.");
            continue;
        }

        if (argsData.ExcludedAuthors is not null && result.Author?.Login is not null && argsData.ExcludedAuthors.Contains(result.Author.Login))
        {
            action.WriteNotice($"[Pull Request {argsData.Org}/{argsData.Repo}#{pullNumber}] Author '{result.Author.Login}' is in excluded list. Skipped.");
            continue;
        }

        tasks.Add(Task.Run(() => ProcessCombinedPrediction(
            categoryPullPredictor,
            servicePullPredictor,
            pullNumber,
            new PullRequest(result),
            argsData.DefaultLabel,
            ModelType.PullRequest,
            argsData.Retries,
            argsData.Test,
            argsData.Threshold
        )));

        action.WriteInfo($"[Pull Request {argsData.Org}/{argsData.Repo}#{pullNumber}] Queued for combined prediction.");
    }
}

var (predictionResults, success) = await App.RunTasks(tasks, action);

foreach (var prediction in predictionResults.OrderBy(p => p.Number))
{
    action.WriteInfo(prediction.ResultMessage);
}

await action.Summary.WritePersistentAsync();
return success ? 0 : 1;

async Task<(ulong Number, string ResultMessage, bool Success)> ProcessCombinedPrediction<T>(
    PredictionEngine<T, LabelPrediction>? categoryPredictor, 
    PredictionEngine<T, LabelPrediction>? servicePredictor, 
    ulong number, 
    T issueOrPull, 
    string? defaultLabel, 
    ModelType type, 
    int[] retries, 
    bool test, 
    float threshold) where T : Issue
{
    List<Action<Summary>> predictionResults = [];
    string typeName = type == ModelType.PullRequest ? "Pull Request" : "Issue";
    List<string> resultMessageParts = [];
    string? error = null;

    (ulong, string, bool) GetResult(bool success)
    {
        foreach (var summaryWrite in predictionResults)
        {
            action.Summary.AddPersistent(summaryWrite);
        }

        return (number, $"[{typeName} {argsData.Org}/{argsData.Repo}#{number}] {string.Join(' ', resultMessageParts)}", success);
    }

    (ulong, string, bool) Success() => GetResult(true);
    (ulong, string, bool) Failure() => GetResult(false);

    predictionResults.Add(summary => summary.AddRawMarkdown($"- **{argsData.Org}/{argsData.Repo}#{number}**", true));

    // if (issueOrPull.HasMoreLabels)
    // {
    //     predictionResults.Add(summary => summary.AddRawMarkdown($"    - Skipping prediction. Too many labels applied already; cannot be sure no applicable label is already applied.", true));
    //     resultMessageParts.Add("Too many labels applied already.");
    //     return Success();
    // }

    // Check if issue already has any labels (might skip prediction if no default label management needed)
    bool hasExistingLabels = issueOrPull.Labels?.Any() ?? false;
    bool hasDefaultLabel = (defaultLabel is not null) && (issueOrPull.Labels?.Any(l => l.Equals(defaultLabel, StringComparison.OrdinalIgnoreCase)) ?? false);

    // // Skip prediction if there are existing labels and no default label to manage
    // if (hasExistingLabels && defaultLabel is null)
    // {
    //     predictionResults.Add(summary => summary.AddRawMarkdown($"    - Skipping prediction. Issue already has labels and no default label specified.", true));
    //     resultMessageParts.Add("Issue already has labels.");
    //     return Success();
    // }

    // Run predictions for both models
    string? categoryLabel = null;
    string? serviceLabel = null;
    float? categoryScore = null;
    float? serviceScore = null;

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
                categoryLabel = bestCategory.Label;
                categoryScore = bestCategory.Score;
                predictionResults.Add(summary => summary.AddRawMarkdown($"    - Category prediction: `{categoryLabel}` (Score: {categoryScore})", true));
            }
            else
            {
                predictionResults.Add(summary => summary.AddRawMarkdown($"    - No category label prediction met the threshold of {threshold}.", true));
            }
        }
        else
        {
            predictionResults.Add(summary => summary.AddRawMarkdown($"    - No category prediction was made.", true));
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
                serviceLabel = bestService.Label;
                serviceScore = bestService.Score;
                predictionResults.Add(summary => summary.AddRawMarkdown($"    - Service prediction: `{serviceLabel}` (Score: {serviceScore})", true));
            }
            else
            {
                predictionResults.Add(summary => summary.AddRawMarkdown($"    - No service label prediction met the threshold of {threshold}.", true));
            }
        }
        else
        {
            predictionResults.Add(summary => summary.AddRawMarkdown($"    - No service prediction was made.", true));
        }
    }

    // Only apply labels if BOTH models have successful predictions
    bool bothModelsRequired = categoryPredictor is not null && servicePredictor is not null;
    bool bothSuccessful = categoryLabel is not null && serviceLabel is not null;
    bool singleModelSuccessful = (categoryPredictor is not null && categoryLabel is not null && servicePredictor is null) ||
                                (servicePredictor is not null && serviceLabel is not null && categoryPredictor is null);

    if ((bothModelsRequired && bothSuccessful) || (!bothModelsRequired && singleModelSuccessful))
    {
        // Apply the predicted labels
        string[] labelsToApply = [];
        if (categoryLabel is not null) labelsToApply.Append(categoryLabel);
        if (serviceLabel is not null) labelsToApply.Append(serviceLabel);

        if (!test)
        {
            error = await GitHubApi.AddLabels(argsData.GitHubToken, argsData.Org, argsData.Repo, typeName, number, labelsToApply, retries, action);
        }

        if (error is null)
        {
            predictionResults.Add(summary => summary.AddRawMarkdown($"    - **`{labelsToApply} applied**", true));
            resultMessageParts.Add($"Labels '{labelsToApply}' applied.");
        }
        else
        {
            predictionResults.Add(summary => summary.AddRawMarkdown($"    - **Error applying labels `{labelsToApply}`**: {error}", true));
            resultMessageParts.Add($"Error occurred applying labels '{labelsToApply}'");
            return Failure();
        }


        // Remove default label if one was applied and we have successful predictions
        if (hasDefaultLabel && defaultLabel is not null)
        {
            if (!test)
            {
                error = await GitHubApi.RemoveLabel(argsData.GitHubToken, argsData.Org, argsData.Repo, typeName, number, defaultLabel, retries, action);
            }

            if (error is null)
            {
                predictionResults.Add(summary => summary.AddRawMarkdown($"    - **Removed default label `{defaultLabel}`**", true));
                resultMessageParts.Add($"Default label '{defaultLabel}' removed.");
            }
            else
            {
                predictionResults.Add(summary => summary.AddRawMarkdown($"    - **Error removing default label `{defaultLabel}`**: {error}", true));
                resultMessageParts.Add($"Error occurred removing default label '{defaultLabel}'");
                return Failure();
            }
        }

        return Success();
    }
    else if (bothModelsRequired && !bothSuccessful)
    {
        predictionResults.Add(summary => summary.AddRawMarkdown($"    - **No labels applied**: Both category and service models must have successful predictions.", true));
        resultMessageParts.Add("No labels applied. Both models must succeed.");

        // Apply default label if needed
        if (defaultLabel is not null && !hasDefaultLabel)
        {
            if (!test)
            {
                error = await GitHubApi.AddLabels(argsData.GitHubToken, argsData.Org, argsData.Repo, typeName, number, [defaultLabel], retries, action);
            }

            if (error is null)
            {
                predictionResults.Add(summary => summary.AddRawMarkdown($"    - **Default label `{defaultLabel}` applied.**", true));
                resultMessageParts.Add($"No prediction made. Default label '{defaultLabel}' applied.");
                return Success();
            }
            else
            {
                predictionResults.Add(summary => summary.AddRawMarkdown($"    - **Error applying default label `{defaultLabel}`**: {error}", true));
                resultMessageParts.Add($"Error occurred applying default label '{defaultLabel}'");
                return Failure();
            }
        }
    }

    resultMessageParts.Add("No prediction made. No applicable label found. No action taken.");
    return GetResult(error is null);
}
