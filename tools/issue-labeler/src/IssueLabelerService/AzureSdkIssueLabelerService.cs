// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using AzureRAGService;
using Azure.Identity;
using System.Linq;

namespace IssueLabelerService
{
    public class AzureSdkIssueLabelerService
    {
        private static readonly ActionResult EmptyResult = new JsonResult(new IssueOutput { Category = "", Service = "", Suggestions = "", Solution = false });
        private readonly ILogger<AzureSdkIssueLabelerService> _logger;
        private readonly IConfiguration _config;
        private readonly ITriageRAG _issueLabeler;

        public AzureSdkIssueLabelerService(IConfiguration config, ILogger<AzureSdkIssueLabelerService> logger, ITriageRAG issueLabeler)
        {
            _config = config;
            _logger = logger;
            _issueLabeler = issueLabeler;
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
                _logger.LogError($"Unable to deserialize payload: {ex.Message}\n\t{ex}");
                return new BadRequestResult();
            }

            IssueOutput result;
            try
            {
                // TODO :  If in dotnet repo run complete issue triage (includes comments) else run the regular triage that we currently do.
                result = CompleteIssueTriage(issue);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to provide labeling and comment for issue: {ex.Message}\n\t{ex}");
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

        private IssueOutput CompleteIssueTriage(IssuePayload issue)
        {
            // Configuration for Azure services
            var credential = new DefaultAzureCredential();
            var searchEndpoint = new Uri(_config["SearchEndpoint"]);
            var openAIEndpoint = new Uri(_config["OpenAIEndpoint"]);
            var modelName = _config["OpenAIModelName"];

            // Issue specific configurations
            var issueIndexName = _config["IssueIndexName"];
            var issueSemanticName = _config["IssueSemanticName"];
            const string issueFieldName = "text_vector";

            // Document specific configurations
            var documentIndexName = _config["DocumentIndexName"];
            var documentSemanticName = _config["DocumentSemanticName"];
            const string documentFieldName = "text_vector";

            string query = $"{issue.Title} {issue.Body}";
            int top = int.Parse(_config["SourceCount"]);
            double scoreThreshold = double.Parse(_config["ScoreThreshold"]);
            double solutionThreshold = double.Parse(_config["SolutionThreshold"]);

            var relevantIssues = _issueLabeler.AzureSearchQuery<Issue>(searchEndpoint, issueIndexName, issueSemanticName, issueFieldName, credential, query, top);
            var relevantDocuments = _issueLabeler.AzureSearchQuery<Document>(searchEndpoint, documentIndexName, documentSemanticName, documentFieldName, credential, query, top);

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
                _logger.LogInformation("Not enough relevant documents/issues found.");
                throw new Exception("Not enough relevant documents/issues found.");
            }

            double highestScore = Math.Max(docs.Max(d => d.Score), issues.Max(d => d.Score));
            bool solution = highestScore >= solutionThreshold;

            _logger.LogInformation($"Highest score: {highestScore}");

            // Makes it nicer for the model to read (Can probably be made more readable but oh well)
            var printableIssues = issues.Select(r => JsonConvert.SerializeObject(r)).ToList();
            var printableDocs = docs.Select(r => JsonConvert.SerializeObject(r)).ToList();

            string instructions, message;
            if (solution)
            {
                instructions = _config["SolutionInstructions"];
                message = $"Sources:\nDocumentation:\n{string.Join("\n", printableDocs)}\nGitHub Issues:\n{string.Join("\n", printableIssues)}\nAs a reminder use ONLY the Category and Service fields from the issues above in your answer.\nProvide the user with a solution to their GitHub Issue:\n{query}";
            }
            else
            {
                instructions = _config["SuggestionInstructions"];
                message = $"Sources:\nDocumentation:\n{string.Join("\n", printableDocs)}\nGitHub Issues:\n{string.Join("\n", printableIssues)}\nAs a reminder use ONLY the Category and Service fields from the issues above in your answer.\nThe user needs suggestions for their GitHub Issue:\n{query}";
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

            var response = _issueLabeler.SendMessageQna(openAIEndpoint, credential, modelName, instructions, message, structure);
            _logger.LogInformation($"Open AI Response : \n{response}");

            var resultObj = JsonConvert.DeserializeObject<AIOutput>(response);
            string intro, outro;

            if (solution)
            {
                intro = $"Hello @{issue.IssueUserLogin}. I'm an AI assistant for the {issue.RepositoryName} repository. I found a solution for your issue!\n";
                outro = "\nThis should solve your problem, if it does not feel free to reopen the issue!";
            }
            else
            {
                intro = $"Hello @{issue.IssueUserLogin}. I'm an AI assistant for the {issue.RepositoryName} repository. I have some suggestions that you can try out while the team gets back to you :)\n\n";
                outro = "\n\nThe team will get back to you shortly, hopefully this helps in the meantime!";
            }

            return new IssueOutput
            {
                Category = resultObj.Category,
                Service = resultObj.Service,
                Suggestions = intro + resultObj.Response + outro,
                Solution = solution
            };
        }

        private class IssuePayload
        {
            public int IssueNumber { get; set; }
            public string Title { get; set; }
            public string Body { get; set; }
            public string IssueUserLogin { get; set; }
            public string RepositoryName { get; set; }
        }

        private class IssueOutput
        {
            public string Category { get; set; }
            public string Service { get; set; }
            public string Suggestions { get; set; }
            public bool Solution { get; set; }
        }

        private class AIOutput
        {
            public string Category { get; set; }
            public string Service { get; set; }
            public string Response { get; set; }
        }
    }
}
