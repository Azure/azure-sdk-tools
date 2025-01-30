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
using System.Collections.Generic;
using System.Linq;

namespace IssueLabelerService
{
    public class AzureSdkIssueLabelerService
    {
        private static readonly ActionResult EmptyResult = new JsonResult(new IssueOutput { Category = "", Service = "", Suggestions = "", Solution = false});
        private readonly ILogger<AzureSdkIssueLabelerService> _logger;
        private IConfiguration Config { get; }
        private ITriageRAG _issueLabeler { get; }

        public AzureSdkIssueLabelerService(IConfiguration config, ILogger<AzureSdkIssueLabelerService> logger, ITriageRAG issueLabeler)
        {
            Config = config;
            _logger = logger;
            _issueLabeler = issueLabeler;
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

            string result;
            try
            {
                // Configurations for correct access in search and OpenAI
                DefaultAzureCredential credential = new();
                Uri searchEndpoint = new Uri(Config["SearchEndpoint"]);
                Uri openAIEndpoint = new Uri(Config["OpenAIEndpoint"]);
                string modelName = Config["OpenAIModelName"];

                // Configuration for Issue specifics
                string issueIndexName = Config["IssueIndexName"];
                string issueSemanticName = Config["IssueSemanticName"];
                string issueFieldName = "text_vector";

                // Configuration for Document specific
                string documentIndexName = Config["DocumentIndexName"];
                string documentSemanticName = Config["DocumentSemanticName"];
                string documentFieldName = "text_vector";

                string query = issue.Title + " " + issue.Body;

                // Top X documents/issues
                int top = 5;

                // Semantic score from 0 - 4, 4 being very relevant
                double scoreThreshold = 2.0;
                double solutionThreshold = 2.8;

                IEnumerable<(Issue, double)> relevantIssues = _issueLabeler.AzureSearchQuery<Issue>(
                    searchEndpoint, issueIndexName, issueSemanticName, issueFieldName, credential, query, top
                );

                IEnumerable<(Document, double)> relevantDocuments = _issueLabeler.AzureSearchQuery<Document>(
                    searchEndpoint, documentIndexName, documentSemanticName, documentFieldName, credential, query, top
                );

                var docs = relevantDocuments.ToList().Select(rd => new
                {
                    Content = rd.Item1.ToString(),
                    Score = rd.Item2
                }).Where(r => r.Score >= scoreThreshold);

                var issues = relevantIssues.ToList().Select(ri => new
                {
                    Content = ri.Item1.ToString(),
                    Score = ri.Item2
                }).Where(r => r.Score >= scoreThreshold);

                string docContent = JsonConvert.SerializeObject(docs);
                string issueContent = JsonConvert.SerializeObject(issues);

                if(docs.Count() == 0 || issues.Count() == 0)
                {
                    _logger.LogInformation("No relevant documents/issues found.");
                    return EmptyResult;
                }

                var highestScore = docs.Concat(issues).Max(r => r.Score);

                string message;

                _logger.LogInformation($"Highest score: {highestScore}");

                if (highestScore >= solutionThreshold)
                {
                    // Prompt to offer a solution
                    message = $"You are an assistant that provides solutions and labels based on the GitHub Issues and the Documentation provided below.\nEach source has an associated score indicating it's relevance to the users query.\nResponse Formatting:\nReturn a JSON with Category, Service, Suggestions, and Solution fields.\n'Solution' will be true.\nThe'Suggestions' field will be the Solution.\n'Suggestions' must prioritize information from the Documentation and will be valid Markdown.\nUse numbered lists or bullet points for organizational purposes.Guidelines:\nUse only the sources provided below.\nProvide the user with the url from sources used.\nProvide the user with the url from sources used.\nUse ONLY the 'Category' and 'Service' fields from the provided GitHub Issues to populate the Category and Service fields in your response.\nStart the solution with the following message: Hello @{issue.IssueUserLogin}. I'm an AI assistant for the {issue.RepositoryName} repository. I found a solution for your issue!\nEnd it with: This should solve your problem, if it does not feel free to reopen the issue!\nSources: Documentation: {docContent}\nGitHub Issues: {issueContent}\nProvide the user with a solution to their GitHub Issue:\n {query}";
                }
                else
                {
                    // Prompt to offer suggestions
                    message = $"You are an assistant that provides suggestions and labels based on the GitHub Issues and the Documentation provided below.\nThe goal is to facilitate the teams job when responding to the issue.\nEach source has an associated score indicating it's relevance to the users query.\nResponse Formatting:\nReturn a JSON with Category, Service, Suggestions, and Solution fields.\n'Solution' will be false.\n'Suggestions' will prioritize information from the Documentation and will be valid Markdown.\nUse numbered lists or bullet points for organizational purposes.\nGuidelines:\nUse only the sources provided below.\nProvide the user with the url from sources used.\nUse ONLY the 'Category' and 'Service' fields from the provided GitHub Issues to populate the Category and Service fields in your response.\nStart the suggestions with the following message: Hello @{issue.IssueUserLogin}. I'm an AI assistant for the {issue.RepositoryName} repository. I have some suggestions that you can try out while the team gets back to you :)\nEnd it with: The team will get back to you shortly, hopefully this helps in the meantime!\nSources:\nDocumentation:\n{docContent}\nGitHub Issues: {issueContent}\nThe user needs suggestions for their GitHub Issue:\n{query}";
                }

                BinaryData structure = BinaryData.FromBytes("""
                        {
                          "type": "object",
                          "properties": {
                            "Category": { "type": "string" },
                            "Service": { "type": "string" },
                            "Suggestions": { "type": "string" },
                            "Solution": { "type": "boolean" }
                          },
                          "required": [ "Category", "Service", "Suggestions", "Solution" ],
                          "additionalProperties": false
                        }
                        """u8.ToArray());

                result = _issueLabeler.SendMessageQna(openAIEndpoint, credential, modelName, message);

                _logger.LogInformation($"Open AI Response : \n{result}");

                result = _issueLabeler.StructureMessage(openAIEndpoint, credential, modelName, result, structure);

                // Model always provides escaped newlines instead of normal ones even though I tell it not too :(
                result = result.Replace("\\n", "\n");

                _logger.LogInformation($"Open AI Structured Response : \n{result}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to label issue:{ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return EmptyResult;
            }

            try
            {
                return new JsonResult(JsonConvert.DeserializeObject<IssueOutput>(result));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to deserialize output:{ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return EmptyResult;
            }
        }

        // Private type used for deserializing the request payload of issue data.
        private class IssuePayload
        {
            public int IssueNumber;
            public string Title;
            public string Body;
            public string IssueUserLogin;
            public string RepositoryName;
        }

        private class IssueOutput
        {
            public string Category { get; set; }
            public string Service { get; set; }
            public string Suggestions { get; set; }
            public bool Solution { get; set; }
        }
    }
}
