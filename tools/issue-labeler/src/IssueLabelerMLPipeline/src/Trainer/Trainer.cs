// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Emit;
using Actions.Core.Extensions;
using Actions.Core.Markdown;
using Actions.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;
using static DataFileUtils;

using var provider = new ServiceCollection()
    .AddGitHubActionsCore()
    .BuildServiceProvider();

var action = provider.GetRequiredService<ICoreService>();

var config = Args.Parse(args, action);
if (config is not Args argsData) return 1;

var success = true;

if (argsData.IssuesDataPath is not null && 
    argsData.CategoryIssuesModelPath is not null && 
    argsData.ServiceIssuesModelPath is not null)
{
    try
    {
        await CreateModel(argsData.IssuesDataPath, argsData.CategoryIssuesModelPath, ModelType.Issue, LabelType.Category, action, argsData.SyntheticIssuesCategoryDataPaths);
        await CreateModel(argsData.IssuesDataPath, argsData.ServiceIssuesModelPath, ModelType.Issue, LabelType.Service, action, argsData.SyntheticIssuesServiceDataPaths);
    }
    catch (Exception ex)
    {
        action.WriteError($"Error training issues models: {ex.Message}");
        action.WriteError($"Stack trace: {ex.StackTrace}");
        success = false;
    }
}

if (argsData.PullsDataPath is not null && 
    argsData.CategoryPullsModelPath is not null && 
    argsData.ServicePullsModelPath is not null)
{
    try
    {
        await CreateModel(argsData.PullsDataPath, argsData.CategoryPullsModelPath, ModelType.PullRequest, LabelType.Category, action, argsData.SyntheticIssuesCategoryDataPaths);
        await CreateModel(argsData.PullsDataPath, argsData.ServicePullsModelPath, ModelType.PullRequest, LabelType.Service, action, argsData.SyntheticIssuesServiceDataPaths);
    }
    catch (Exception ex)
    {
        action.WriteError($"Error training pull request models: {ex.Message}");
        action.WriteError($"Stack trace: {ex.StackTrace}");
        success = false;
    }
}

return success ? 0 : 1;

static async Task CreateModel(string dataPath, string modelPath, ModelType type, LabelType labelType, ICoreService action, string[]? syntheticIssuesDataPaths = null)
{
    if (!File.Exists(dataPath))
    {
        action.WriteNotice($"The data file '{dataPath}' does not exist.");
        action.Summary.AddPersistent(summary => summary.AddAlert("The data file does not exist. Training cannot proceed.", AlertType.Caution));
        await action.Summary.WriteAsync();

        throw new InvalidOperationException($"The data file '{dataPath}' does not exist.");
    }

    int recordsCounted = File.ReadLines(dataPath).Take(10).Count();
    if (recordsCounted < 10)
    {
        action.WriteNotice($"The data file '{dataPath}' does not contain enough data for training. A minimum of 10 records is required, but only {recordsCounted} exist.");
        action.Summary.AddPersistent(summary => summary.AddAlert($"Only {recordsCounted} items were found to be used for training. A minimum of 10 records is required. Cannot proceed with training.", AlertType.Caution));
        await action.Summary.WriteAsync();

        throw new InvalidOperationException($"The data file '{dataPath}' does not contain enough data for training. A minimum of 10 records is required, but only {recordsCounted} exist.");
    }

    await action.WriteStatusAsync($"Loading data into train/test sets for {labelType} labels...");
    MLContext mlContext = new();
    string columnName = labelType == LabelType.Category ? "CategoryLabel" : "ServiceLabel";
    TextLoader.Column labelColumn = labelType == LabelType.Category ? new(columnName, DataKind.String, 0) : new(columnName, DataKind.String, 1);
    TextLoader.Column[] columns = type == ModelType.Issue ? [
        labelColumn,
        new("Title", DataKind.String, 2),
        new("Body", DataKind.String, 3),
    ] : [
        labelColumn,
        new("Title", DataKind.String, 2),
        new("Body", DataKind.String, 3),
        new("FileNames", DataKind.String, 4),
        new("FolderNames", DataKind.String, 5)
    ];

    TextLoader.Options textLoaderOptions = new()
    {
        AllowQuoting = false,
        AllowSparse = false,
        EscapeChar = '"',
        HasHeader = true,
        ReadMultilines = false,
        Separators = ['\t'],
        TrimWhitespace = true,
        UseThreads = true,
        Columns = columns
    };

    var loader = mlContext.Data.CreateTextLoader(textLoaderOptions);
    var dataPaths = syntheticIssuesDataPaths is not null ? syntheticIssuesDataPaths.Append(dataPath) : [dataPath];
    var data = loader.Load([.. dataPaths]);
    var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

    await action.WriteStatusAsync("Building pipeline...");

    var xf = mlContext.Transforms;
    var pipeline = xf.Conversion.MapValueToKey(inputColumnName: columnName, outputColumnName: "LabelKey")
        .Append(xf.Text.FeaturizeText(
            "Features",
            new TextFeaturizingEstimator.Options(),
            columns.Select(c => c.Name).ToArray()))
        .AppendCacheCheckpoint(mlContext)
        .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("LabelKey"))
        .Append(xf.Conversion.MapKeyToValue("PredictedLabel"));

    await action.WriteStatusAsync("Fitting the model with the training data set...");
    var trainedModel = pipeline.Fit(split.TrainSet);
    var testModel = trainedModel.Transform(split.TestSet);

    await action.WriteStatusAsync("Evaluating against the test set...");
    var metrics = mlContext.MulticlassClassification.Evaluate(testModel, labelColumnName: "LabelKey");

    action.WriteInfo($"************************************************************");
    action.WriteInfo($"MacroAccuracy = {metrics.MacroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
    action.WriteInfo($"MicroAccuracy = {metrics.MicroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
    action.WriteInfo($"LogLoss = {metrics.LogLoss:0.####}, the closer to 0, the better");

    // Find the original label values.
    try
    {
        VBuffer<ReadOnlyMemory<char>> labelNames = default;
        trainedModel.GetOutputSchema(split.TrainSet.Schema)["LabelKey"].GetKeyValues(ref labelNames);
        var originalLabels = labelNames.DenseValues().Select(x => x.ToString()).ToArray();
        List<string> labelsWithHighLogLoss = [];

        for (int i = 0; i < metrics.PerClassLogLoss.Count() && i < originalLabels.Length; i++)
        {
            action.WriteInfo($"LogLoss for '{originalLabels[i]}' = {metrics.PerClassLogLoss[i]:0.####}");
            if (metrics.PerClassLogLoss[i] > 2)
            {
                labelsWithHighLogLoss.Add(originalLabels[i]);
            }
        }
        action.WriteInfo($"Number of classes: {metrics.PerClassLogLoss.Count()}");
        action.WriteInfo($"Classes with Logloss > 2: {string.Join(", ", labelsWithHighLogLoss)}");
        
        action.Summary.AddPersistent(summary =>
        {
            summary.AddMarkdownHeading($"Finished Training {(type == ModelType.Issue ? "Issues" : "Pull Requests")} {labelType} Model", 2);

            summary.AddRawMarkdown($"""
                * MacroAccuracy: {metrics.MacroAccuracy:0.####} (a value between 0 and 1; the closer to 1, the better)
                * MicroAccuracy: {metrics.MicroAccuracy:0.####} (a value between 0 and 1; the closer to 1, the better)
                * LogLoss: {metrics.LogLoss:0.####} (the closer to 0, the better)
                """);
            for (int i = 0; i < metrics.PerClassLogLoss.Count() && i < originalLabels.Length; i++)
            {
                summary.AddRawMarkdown($"LogLoss for '{originalLabels[i]}' = {metrics.PerClassLogLoss[i]:0.####}");
            }
        });

        await action.Summary.WriteAsync();
    }
    catch (Exception ex)
    {
        action.WriteInfo($"Could not retrieve class-specific log loss with label names: {ex.Message}");
        for (int i = 0; i < metrics.PerClassLogLoss.Count(); i++)
        {
            action.WriteInfo($"LogLoss for class {i} = {metrics.PerClassLogLoss[i]:0.####}");
        }
    }

    action.WriteInfo($"************************************************************");

    action.WriteInfo($"Saving model to '{modelPath}'...");
    EnsureOutputDirectory(modelPath);
    mlContext.Model.Save(trainedModel, split.TrainSet.Schema, modelPath);
}
