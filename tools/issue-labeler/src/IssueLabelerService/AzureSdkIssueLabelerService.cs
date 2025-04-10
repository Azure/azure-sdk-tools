// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public class AzureSdkIssueLabelerService
    {
        private static readonly ActionResult EmptyResult = new JsonResult(new TriageOutput { Labels = [], Answer = null, AnswerType = null });
        private readonly ILogger<AzureSdkIssueLabelerService> _logger;
        private readonly TriageRag _ragService;
        private readonly Configuration _configurationService;
        private Labelers _labelers;

        public AzureSdkIssueLabelerService(ILogger<AzureSdkIssueLabelerService> logger, TriageRag ragService, Configuration configService, Labelers labelers)
        {
            _logger = logger;
            _ragService = ragService;
            _labelers = labelers;
            _configurationService = configService;
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
                _logger.LogError($"Unable to deserialize payload: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return new BadRequestResult();
            }

            var config = _configurationService.GetForRepository($"{issue.RepositoryOwnerName}/{issue.RepositoryName}");

            try
            {
                // Get the labeler based on the configuration
                var labeler = _labelers.GetLabeler(config);

                // Predict labels for the issue
                string[] labels = await labeler.PredictLabels(issue);

                // If no labels are returned, do not generate an answer
                if (labels == null || labels.Length == 0)
                {
                    _logger.LogInformation($"No labels predicted for issue #{issue.IssueNumber} in repository {issue.RepositoryName}.");
                    return EmptyResult;
                }

                // Proceed to generate content if labels are available
                var result = await CompleteIssueTriageAsync(issue, config);
                result.Labels = labels;

                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing issue #{issue.IssueNumber} in repository {issue.RepositoryName}: {ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return EmptyResult;
            }
        }

        private async Task<IssuePayload> DeserializeIssuePayloadAsync(HttpRequest request)
        {
            using var bodyReader = new StreamReader(request.Body);
            var requestBody = await bodyReader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<IssuePayload>(requestBody);
        }

        private async Task<TriageOutput> CompleteIssueTriageAsync(IssuePayload issue, RepositoryConfiguration config)
        {
            // Configuration for Azure services
            var modelName = config.OpenAIModelName;
            var issueIndexName = config.IssueIndexName;
            var documentIndexName = config.DocumentIndexName;

            // Issue specific configurations
            var issueSemanticName = config.IssueSemanticName;
            const string issueFieldName = "text_vector";

            // Document specific configurations
            var documentSemanticName = config.DocumentSemanticName;
            const string documentFieldName = "text_vector";

            // Query + Filtering configurations
            string query = $"{issue.Title} {issue.Body}";
            int top = int.Parse(config.SourceCount);
            double scoreThreshold = double.Parse(config.ScoreThreshold);
            double solutionThreshold = double.Parse(config.SolutionThreshold);

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
            if (docs.Count() == 0 || issues.Count() == 0)
            {
                throw new Exception($"Not enough relevant sources found for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.");
            }

            double highestScore = Math.Max(docs.Max(d => d.Score), issues.Max(d => d.Score));
            bool solution = highestScore >= solutionThreshold;

            _logger.LogInformation($"Highest relevance score among the sources: {highestScore}");

            // Makes it nicer for the model to read (Can probably be made more readable but oh well)
            var printableIssues = issues.Select(r => JsonConvert.SerializeObject(r)).ToList();
            var printableDocs = docs.Select(r => JsonConvert.SerializeObject(r)).ToList();

            string instructions = config.Instructions;
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
            return new TriageOutput
            {
                Labels = [ resultObj.Service, resultObj.Category ],
                Answer = formatted_response,
                AnswerType = solution ? "solution" : "suggestion",
            };
        }
    }
}
