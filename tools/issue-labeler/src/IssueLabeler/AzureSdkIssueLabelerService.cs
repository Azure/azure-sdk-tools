using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hubbup.MikLabelModel;
using IssueLabeler.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace IssueLabeler
{
    public class AzureSdkIssueLabelerService
    {
        private static readonly ActionResult EmptyResult = new JsonResult(new PredictionResponse(Array.Empty<string>()));
        private static readonly ConcurrentDictionary<string, byte> CommonModelRepositories = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> InitializedRepositories = new(StringComparer.OrdinalIgnoreCase);

        private readonly ILogger<AzureSdkIssueLabelerService> _logger;

        private string CommonModelRepositoryName { get; }
        private ILabelerLite Labeler { get; }
        private IConfiguration Config { get; }
        private IModelHolderFactoryLite ModelHolderFactory { get; }

        public AzureSdkIssueLabelerService(ILabelerLite labeler, IModelHolderFactoryLite modelHolderFactory, IConfiguration config, ILogger<AzureSdkIssueLabelerService> logger)
        {
            ModelHolderFactory = modelHolderFactory;
            Labeler = labeler;
            Config = config;
            _logger = logger;

            CommonModelRepositoryName = config["CommonModelRepositoryName"];

            // Initialize the set of repositories that use the common model.

            var commonModelRepos = config["ReposUsingCommonModel"];

            if (!string.IsNullOrEmpty(commonModelRepos))
            {
                foreach (var repo in commonModelRepos.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    CommonModelRepositories.TryAdd(repo, 1);
                }
            }
        }

        [Function("AzureSdkIssueLabelerService")]
        public async Task<ActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "POST", Route = null)] HttpRequest request)
        {
            IssuePayload issue;

            try
            {
                using var bodyReader = new StreamReader(request.Body);

                var requestBody = await bodyReader.ReadToEndAsync();
                issue = JsonConvert.DeserializeObject<IssuePayload>(requestBody);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to deserialize payload:{ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return new BadRequestResult();
            }

            var predictionRepositoryName = TranslateRepoName(issue.RepositoryName);

            // If the model needed for this request hasn't been initialized, do so now.
            if (!InitializedRepositories.ContainsKey(predictionRepositoryName))
            {
                _logger.LogInformation($"Models for {predictionRepositoryName} have not yet been initialized; loading prediction models.");

                try
                {
                    var allBlobConfigNames = Config[$"IssueModel.{predictionRepositoryName.Replace("-", "_")}.BlobConfigNames"].Split(';', StringSplitOptions.RemoveEmptyEntries);

                    // The model factory is thread-safe and will manage its own concurrency.
                    await ModelHolderFactory.CreateModelHolders(issue.RepositoryOwnerName, predictionRepositoryName, allBlobConfigNames).ConfigureAwait(false);
                    InitializedRepositories.TryAdd(predictionRepositoryName, 1);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error initializing the label prediction models for {predictionRepositoryName}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                    return EmptyResult;
                }
                finally
                {
                    _logger.LogInformation($"Model initialization is complete for {predictionRepositoryName}.");
                }
            }

            // Predict labels.
            _logger.LogInformation($"Predicting labels for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}.");

            try
            {
                // In order for labels to be valid for Azure SDK use, there must
                // be at least two of them, which corresponds to a Service (pink)
                // and Category (yellow).  If that is not met, then no predictions
                // should be returned.
                var predictions = await Labeler.QueryLabelPrediction(
                    issue.IssueNumber,
                    issue.Title,
                    issue.Body,
                    issue.IssueUserLogin,
                    predictionRepositoryName,
                    issue.RepositoryOwnerName);

                if (predictions.Count < 2)
                {
                    _logger.LogInformation($"No labels were predicted for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}.");
                    return EmptyResult;
                }

                _logger.LogInformation($"Labels were predicted for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}.  Using: [{predictions[0]}, {predictions[1]}].");
                return new JsonResult(new PredictionResponse(predictions));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error querying predictions for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return EmptyResult;
            }
        }

        private string TranslateRepoName(string repoName) =>
            CommonModelRepositories.ContainsKey(repoName)
            ? CommonModelRepositoryName
            : repoName;

        // Private type used for deserializing the request payload of issue data.
        private class IssuePayload
        {
            public int IssueNumber;
            public string Title;
            public string Body;
            public string IssueUserLogin;
            public string RepositoryName;
            public string RepositoryOwnerName;
        }

        // Type used for shaping the JSON response payload.
        public record PredictionResponse(IEnumerable<string> labels);
    }
}