// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;
using static DataFileUtils;

/// <summary>
/// Provides methods for training ML models for issue and pull request label prediction.
/// </summary>
public static class ModelTrainer
{
    /// <summary>
    /// Creates and trains an ML model from a TSV data file.
    /// </summary>
    /// <param name="dataPath">The path to the TSV data file.</param>
    /// <param name="modelPath">The path where the trained model will be saved.</param>
    /// <param name="modelType">The type of model (Issue or PullRequest).</param>
    /// <param name="labelType">The type of label to predict (Category or Service).</param>
    /// <param name="syntheticDataPaths">Optional paths to additional synthetic data files.</param>
    /// <exception cref="InvalidOperationException">Thrown when data file doesn't exist or has insufficient records.</exception>
    public static void CreateModel(
        string dataPath,
        string modelPath,
        ModelType modelType,
        LabelType labelType,
        string[]? syntheticDataPaths = null)
    {
        if (!File.Exists(dataPath))
        {
            Console.WriteLine($"ERROR: The data file '{dataPath}' does not exist.");
            throw new InvalidOperationException($"The data file '{dataPath}' does not exist.");
        }

        int recordsCounted = File.ReadLines(dataPath).Skip(1).Take(10).Count(); // Skip header
        if (recordsCounted < 10)
        {
            Console.WriteLine($"ERROR: The data file '{dataPath}' does not contain enough data for training. A minimum of 10 records is required, but only {recordsCounted} exist.");
            throw new InvalidOperationException($"The data file '{dataPath}' does not contain enough data for training. A minimum of 10 records is required, but only {recordsCounted} exist.");
        }

        Console.WriteLine($"Loading data into train/test sets for {labelType} labels...");
        MLContext mlContext = new();
        string columnName = labelType == LabelType.Category ? "CategoryLabel" : "ServiceLabel";
        TextLoader.Column labelColumn = labelType == LabelType.Category 
            ? new(columnName, DataKind.String, 0) 
            : new(columnName, DataKind.String, 1);
        
        TextLoader.Column[] columns = modelType == ModelType.Issue 
            ? [
                labelColumn,
                new("Title", DataKind.String, 2),
                new("Description", DataKind.String, 3),
            ] 
            : [
                labelColumn,
                new("Title", DataKind.String, 2),
                new("Description", DataKind.String, 3),
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
        var dataPaths = syntheticDataPaths is not null 
            ? [.. syntheticDataPaths, dataPath] 
            : new[] { dataPath };
        var data = loader.Load(dataPaths);
        var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

        Console.WriteLine("Building pipeline...");

        var xf = mlContext.Transforms;
        var pipeline = xf.Conversion.MapValueToKey(inputColumnName: columnName, outputColumnName: "LabelKey")
            .Append(xf.Text.FeaturizeText(
                "Features",
                new TextFeaturizingEstimator.Options(),
                columns.Select(c => c.Name).ToArray()))
            .AppendCacheCheckpoint(mlContext)
            .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("LabelKey"))
            .Append(xf.Conversion.MapKeyToValue("PredictedLabel"));

        Console.WriteLine("Fitting the model with the training data set...");
        var trainedModel = pipeline.Fit(split.TrainSet);
        var testModel = trainedModel.Transform(split.TestSet);

        Console.WriteLine("Evaluating against the test set...");
        var metrics = mlContext.MulticlassClassification.Evaluate(testModel, labelColumnName: "LabelKey");

        Console.WriteLine($"************************************************************");
        Console.WriteLine($"MacroAccuracy = {metrics.MacroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
        Console.WriteLine($"MicroAccuracy = {metrics.MicroAccuracy:0.####}, a value between 0 and 1, the closer to 1, the better");
        Console.WriteLine($"LogLoss = {metrics.LogLoss:0.####}, the closer to 0, the better");

        // Find the original label values.
        try
        {
            VBuffer<ReadOnlyMemory<char>> labelNames = default;
            trainedModel.GetOutputSchema(split.TrainSet.Schema)["LabelKey"].GetKeyValues(ref labelNames);
            var originalLabels = labelNames.DenseValues().Select(x => x.ToString()).ToArray();
            List<string> labelsWithHighLogLoss = [];

            for (int i = 0; i < metrics.PerClassLogLoss.Count() && i < originalLabels.Length; i++)
            {
                Console.WriteLine($"LogLoss for '{originalLabels[i]}' = {metrics.PerClassLogLoss[i]:0.####}");
                if (metrics.PerClassLogLoss[i] > 2)
                {
                    labelsWithHighLogLoss.Add(originalLabels[i]);
                }
            }
            Console.WriteLine($"Number of classes: {metrics.PerClassLogLoss.Count()}");
            Console.WriteLine($"Classes with Logloss > 2: {string.Join(", ", labelsWithHighLogLoss)}");

            Console.WriteLine();
            Console.WriteLine($"=== Finished Training {(modelType == ModelType.Issue ? "Issues" : "Pull Requests")} {labelType} Model ===");
            Console.WriteLine($"* MacroAccuracy: {metrics.MacroAccuracy:0.####} (a value between 0 and 1; the closer to 1, the better)");
            Console.WriteLine($"* MicroAccuracy: {metrics.MicroAccuracy:0.####} (a value between 0 and 1; the closer to 1, the better)");
            Console.WriteLine($"* LogLoss: {metrics.LogLoss:0.####} (the closer to 0, the better)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not retrieve class-specific log loss with label names: {ex.Message}");
            for (int i = 0; i < metrics.PerClassLogLoss.Count(); i++)
            {
                Console.WriteLine($"LogLoss for class {i} = {metrics.PerClassLogLoss[i]:0.####}");
            }
        }

        Console.WriteLine($"************************************************************");

        Console.WriteLine($"Saving model to '{modelPath}'...");
        EnsureOutputDirectory(modelPath);
        mlContext.Model.Save(trainedModel, split.TrainSet.Schema, modelPath);
    }
}
