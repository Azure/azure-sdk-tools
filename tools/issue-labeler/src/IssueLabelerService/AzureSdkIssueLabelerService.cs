// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Hubbup.MikLabelModel;
using IssueLabeler.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using AzureRagService;
using Azure.Identity;
using System.Linq;

namespace IssueLabelerService
{
    public class AzureSdkIssueLabelerService
    {
        private static readonly ActionResult EmptyResult = new JsonResult(new IssueOutput { Labels = [], Answer = null, AnswerType = null });
        private readonly ILogger<AzureSdkIssueLabelerService> _logger;
        private readonly IConfiguration _config;
        private readonly TriageRag _ragService;
        private static readonly ConcurrentDictionary<string, byte> CommonModelRepositories = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> InitializedRepositories = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> CompleteTraigeRepositories = new(StringComparer.OrdinalIgnoreCase); 
        private IModelHolderFactoryLite ModelHolderFactory { get; }
        private ILabelerLite Labeler { get; }
        private string CommonModelRepositoryName { get; }

        public AzureSdkIssueLabelerService(ILabelerLite labeler, IModelHolderFactoryLite modelHolderFactory, IConfiguration config, ILogger<AzureSdkIssueLabelerService> logger, TriageRag ragService)
        {
            _config = config;
            _logger = logger;
            _ragService = ragService;
            ModelHolderFactory = modelHolderFactory;
            Labeler = labeler;

            CommonModelRepositoryName = config["CommonModelRepositoryName"];

            // Initialize the set of repositories that use the common model.
            ConvertRepoStringList(config["ReposUsingCommonModel"], CommonModelRepositories);
            ConvertRepoStringList(config["CompleteTraigeRepositories"], CompleteTraigeRepositories);
        }

        [Function("AzureSdkIssueLabelerService")]
        public async Task<ActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "POST", Route = null)] HttpRequest request)
        {         
            IssuePayload issue;
            try
            {
                issue = await DeserializeIssuePayloadAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to deserialize payload:{ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return new BadRequestResult();
            }

            IssueOutput result;
            try
            {
                // If in flagged triage repo run complete issue triage (includes comments) otherwise run the regular triage that we currently do.
                if (CompleteTraigeRepositories.ContainsKey(issue.RepositoryName))
                {
                    try
                    {
                        result = await CompleteIssueTriageAsync(issue);

                        // Temporary run both to get labels
                        if(issue.RepositoryName == "azure-sdk-for-net")
                        {
                            var labels = await OnlyLabelIssueAsync(issue);
                            result.Labels = labels.Labels;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Complete Triage failed for {issue.RepositoryName} on issue #{issue.IssueNumber}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                        
                        // Attempt to just label the issue
                        _logger.LogInformation($"Attempting to run labeler on issue #{issue.IssueNumber}.");
                        result = await OnlyLabelIssueAsync(issue);
                    }
                }
                else
                {
                    result = await OnlyLabelIssueAsync(issue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error querying predictions for {issue.RepositoryName} on issue #{issue.IssueNumber}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return EmptyResult;
            }

            try
            {
                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to deserialize output: {ex.Message}\n\t{ex}");
                return EmptyResult;
            }
        }

        private async Task<IssuePayload> DeserializeIssuePayloadAsync(HttpRequest request)
        {
            using var bodyReader = new StreamReader(request.Body);
            var requestBody = await bodyReader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<IssuePayload>(requestBody);
        }

        private async Task<IssueOutput> CompleteIssueTriageAsync(IssuePayload issue)
        {
            // Configuration for Azure services
            var modelName = _config["OpenAIModelName"];

            // TODO make switching between different Indexes easier
            // For now we manually go python or dotnet
            var issueIndexName = _config["IssueIndexNameDotNet"];
            var documentIndexName = _config["DocumentIndexNameDotNet"];
            if (issue.RepositoryName == "azure-sdk-for-python")
            {
                issueIndexName = _config["IssueIndexNamePython"];
                documentIndexName = _config["DocumentIndexNamePython"];
            }

            // Issue specific configurations
            var issueSemanticName = _config["IssueSemanticName"];
            const string issueFieldName = "text_vector";

            // Document specific configurations
            var documentSemanticName = _config["DocumentSemanticName"];
            const string documentFieldName = "text_vector";

            // Query + Filtering configurations
            string query = $"{issue.Title} {issue.Body}";
            int top = int.Parse(_config["SourceCount"]);
            double scoreThreshold = double.Parse(_config["ScoreThreshold"]);
            double solutionThreshold = double.Parse(_config["SolutionThreshold"]);

            var relevantIssues = await _ragService.AzureSearchQueryAsync<Issue>(issueIndexName, issueSemanticName, issueFieldName, query, top);
            var relevantDocuments = await _ragService.AzureSearchQueryAsync<Document>(documentIndexName, documentSemanticName, documentFieldName, query, top);

            // Filter sources under threshold
            var docs = relevantDocuments
                .Where(r => r.Item2 >= scoreThreshold)
                .Select(rd => new
                {
                    rd.Item1.chunk,
                    rd.Item1.Url,
                    Score = rd.Item2
                })
                .ToList();

            var issues = relevantIssues
                .Where(r => r.Item2 >= scoreThreshold)
                .Select(rd => new
                {
                    rd.Item1.Title,
                    rd.Item1.chunk,
                    rd.Item1.Service,
                    rd.Item1.Category,
                    rd.Item1.Url,
                    Score = rd.Item2
                })
                .ToList();

            // Filtered out all sources for either one then not enough information to answer the issue. 
            if (docs.Count == 0 || issues.Count == 0)
            {
                throw new Exception($"Not enough relevant sources found for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.");
            }

            double highestScore = Math.Max(docs.Max(d => d.Score), issues.Max(d => d.Score));
            bool solution = highestScore >= solutionThreshold;

            _logger.LogInformation($"Highest relevance score among the sources: {highestScore}");

            // Makes it nicer for the model to read (Can probably be made more readable but oh well)
            var printableIssues = issues.Select(r => JsonConvert.SerializeObject(r)).ToList();
            var printableDocs = docs.Select(r => JsonConvert.SerializeObject(r)).ToList();

            string instructions = _config["Instructions"];
            string message;
            if (solution)
            {
                message = $"Sources:\nDocumentation:\n{string.Join("\n", printableDocs)}\nGitHub Issues:\n{string.Join("\n", printableIssues)}\nThe user needs a solution to their GitHub Issue:\n{query}";
            }
            else
            {
                message = $"Sources:\nDocumentation:\n{string.Join("\n", printableDocs)}\nGitHub Issues:\n{string.Join("\n", printableIssues)}\nThe user needs suggestions for their GitHub Issue:\n{query}";
            }

            // Structured output for the model
            var structure = BinaryData.FromBytes("""
            {
              "type": "object",
              "properties": {
                "Category": { "type": "string" },
                "Service": { "type": "string" },
                "Response": { "type": "string" }
              },
              "required": [ "Category", "Service", "Response" ],
              "additionalProperties": false
            }
            """u8.ToArray());

            var response = await _ragService.SendMessageQnaAsync(instructions, message, structure);
            _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.: \n{response}");

            var resultObj = JsonConvert.DeserializeObject<AIOutput>(response);
            string intro, outro;

            if (solution)
            {
                intro = $"Hello @{issue.IssueUserLogin}. I'm an AI assistant for the {issue.RepositoryName} repository. I found a solution for your issue!\n\n";
                outro = "\n\nThis should solve your problem, if it does not feel free to reopen the issue.";
            }
            else
            {
                intro = $"Hello @{issue.IssueUserLogin}. I'm an AI assistant for the {issue.RepositoryName} repository. I have some suggestions that you can try out while the team gets back to you.\n\n";
                outro = "\n\nThe team will get back to you shortly, hopefully this helps in the meantime.";
            }

            if (string.IsNullOrEmpty(resultObj.Response))
            {
                throw new Exception($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber} had an emtpy response.");
            }

            string formatted_response = intro + resultObj.Response + outro;
            return new IssueOutput
            {
                Labels = [ resultObj.Service, resultObj.Category ],
                Answer = formatted_response,
                AnswerType = solution ? "solution" : "suggestion",
            };
        }

        private async Task<IssueOutput> OnlyLabelIssueAsync(IssuePayload issue)
        {
            var predictionRepositoryName = TranslateRepoName(issue.RepositoryName);

            // If the model needed for this request hasn't been initialized, do so now.
            if (!InitializedRepositories.ContainsKey(predictionRepositoryName))
            {
                _logger.LogInformation($"Models for {predictionRepositoryName} have not yet been initialized; loading prediction models.");

                try
                {
                    var allBlobConfigNames = _config[$"IssueModel.{predictionRepositoryName.Replace("-", "_")}.BlobConfigNames"].Split(';', StringSplitOptions.RemoveEmptyEntries);

                    // The model factory is thread-safe and will manage its own concurrency.
                    await ModelHolderFactory.CreateModelHolders(issue.RepositoryOwnerName, predictionRepositoryName, allBlobConfigNames).ConfigureAwait(false);
                    InitializedRepositories.TryAdd(predictionRepositoryName, 1);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error initializing the label prediction models for {predictionRepositoryName}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                    throw;
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
                    throw new Exception($"No labels were predicted for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}.");
                }

                _logger.LogInformation($"Labels were predicted for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}.  Using: [{predictions[0]}, {predictions[1]}].");
                return new IssueOutput
                {
                    Labels = [ predictions[0], predictions[1] ],
                    Answer = null,
                    AnswerType = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error querying predictions for {issue.RepositoryName} using the `{predictionRepositoryName}` model for issue #{issue.IssueNumber}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                throw;
            }
        }

        private string TranslateRepoName(string repoName) =>
            CommonModelRepositories.ContainsKey(repoName)
            ? CommonModelRepositoryName
            : repoName;

        private void ConvertRepoStringList(string repos, ConcurrentDictionary<string, byte> dict)
        {
            if (!string.IsNullOrEmpty(repos))
            {
                foreach (var repo in repos.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    dict.TryAdd(repo, 1);
                }
            }
        }

        private class IssuePayload
        {
            public int IssueNumber { get; set; }
            public string Title { get; set; }
            public string Body { get; set; }
            public string IssueUserLogin { get; set; }
            public string RepositoryName { get; set; }
            public string RepositoryOwnerName { get; set; }
        }

        // Structure of output fed to the github event processor
        private class IssueOutput
        {
            public string[] Labels { get; set; }
            public string Answer { get; set; }
            public string AnswerType { get; set; }
        }

        // Structure of OpenAI Response  
        private class AIOutput
        {
            public string Category { get; set; }
            public string Service { get; set; }
            public string Response { get; set; }
        }
    }
}