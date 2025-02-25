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
            // Deserialize the Issue that's coming in.
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

            IssueOutput result;
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

                // Found that Search performs better when given the title and body as a single string rather than a full JSON.
                // Nothing to back that up except just trying it a bunch of times :)
                string query = issue.Title + " " + issue.Body;

                // Top X documents/issues
                // This becomes very dependent on Chunk size -> smaller chunks means more documents/issues and vice versa.
                int top = 50;

                // Semantic score from 0 - 4, 4 being very relevant
                // Anything under 2 is not relevant enough to be used.
                double scoreThreshold = 2.0;

                // Arbituary Number I chose to indicate that one of the retrieved documents/issues is relevant 
                // enough to formulate a solution from.
                double solutionThreshold = 3.0;

                IEnumerable<(Issue, double)> relevantIssues = _issueLabeler.AzureSearchQuery<Issue>(
                    searchEndpoint, issueIndexName, issueSemanticName, issueFieldName, credential, query, top
                );

                IEnumerable<(Document, double)> relevantDocuments = _issueLabeler.AzureSearchQuery<Document>(
                    searchEndpoint, documentIndexName, documentSemanticName, documentFieldName, credential, query, top
                );

                // Filter out documents/issues that don't meet the minimum score threshold
                var docs = relevantDocuments.Where(r => r.Item2 >= scoreThreshold).Select(rd => new
                {
                    rd.Item1.chunk,
                    rd.Item1.Url,
                    Score = rd.Item2
                }).ToList();

                var issues = relevantIssues.Where(r => r.Item2 >= scoreThreshold).Select(rd => new
                {
                    rd.Item1.Title,
                    rd.Item1.chunk,
                    rd.Item1.Service,
                    rd.Item1.Category,
                    rd.Item1.Url,
                    Score = rd.Item2
                }).ToList();

                // If no relevant documents/issues are found, return an empty result
                if(docs.Count == 0 || issues.Count == 0)
                {
                    _logger.LogInformation("Not enough relevant documents/issues found.");
                    return EmptyResult;
                }

                // If theres a document with a score higher than the solution threshold, we can assume that a solution can be provided.
                // Debatable wether or not we should consider Issue relevance since it can be less reliable to provide a solution overall.
                var docsMax = docs.Max(d => d.Score);
                var issuesMax = issues.Max(d => d.Score);
                var highestScore = docsMax >= issuesMax ? docsMax : issuesMax;

                bool solution = highestScore >= solutionThreshold;

                _logger.LogInformation($"Highest score: {highestScore}");
                
                // Improves readability when giving to the model.
                var printableIssues = issues.Select(r => JsonConvert.SerializeObject(r)).ToList();
                var printableDocs = docs.Select(r => JsonConvert.SerializeObject(r)).ToList();


                // Splitting the message into instructions to take advantage of developer instructions in o3-mini
                string instructions;
                string message;
                if (solution)
                {
                    // Prompt to offer a solution
                    instructions = $"You are an Expierenced Developer in the Azure SDK Team that provides solutions and labels for Github Issues. The response field must prioritize Documentation over Issues and will be valid Markdown with organized content using bullet points and numeric lists. Use only the sources provided and cite your sources with the given URL's. GitHub Issues provided are in JSON format and have 'Category' and 'Service' fields. Use ONLY those fields to populate the Category and Service fields in your answer. No need for introductions or outros because they are added later on. Each source has an associated score indicating it's relevance to the users query.";
                    message = $"Sources:\nDocumentation:\n{string.Join("\n", printableDocs)}\nGitHub Issues:\n{string.Join("\n", printableIssues)}\nProvide the user with a solution to their GitHub Issue:\n{query}";
                }
                else
                {
                    // Prompt to offer suggestions
                    instructions = $"You are an Expierenced Developer in the Azure SDK Team that provides suggestions and labels for Github Issues. The response field must prioritize Documentation over Issues and will be valid Markdown with organized content using bullet points and numeric lists. Use only the sources provided and cite your sources with the given URL's. GitHub Issues provided are in JSON format and have 'Category' and 'Service' fields. Use ONLY those fields to populate the Category and Service fields in your answer. No need for introductions or outros because they are added later on. Each source has an associated score indicating it's relevance to the users query.";
                    message = $"\nSources:\nDocumentation:\n{string.Join("\n", printableDocs)}\nGitHub Issues:\n{string.Join("\n", printableIssues)}\nThe user needs suggestions for their GitHub Issue:\n{query}";
                }

                // Structure of the response passed to the StructureMessage method.
                BinaryData structure = BinaryData.FromBytes("""
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

                response = _issueLabeler.StructureMessage(openAIEndpoint, credential, modelName, response, structure);

                // Removing complexity from the prompt by dealing with intro, outro, and solution outside of the prompt. Avoids hallucinations.
                if (solution)
                {
                    var resultObj = JsonConvert.DeserializeObject<AIOutput>(response);

                    string intro = $"Hello @{issue.IssueUserLogin}. I'm an AI assistant for the {issue.RepositoryName} repository. I found a solution for your issue!\n";
                    string outro = "\nThis should solve your problem, if it does not feel free to reopen the issue!";

                    result = new IssueOutput
                    {
                        Category = resultObj.Category,
                        Service = resultObj.Service,
                        Suggestions = intro + resultObj.Response + outro,
                        Solution = solution
                    };
                }
                else
                {
                    var resultObj = JsonConvert.DeserializeObject<AIOutput>(response);

                    string intro = $"Hello @{issue.IssueUserLogin}. I'm an AI assistant for the {issue.RepositoryName} repository. I have some suggestions that you can try out while the team gets back to you :)\n\n";
                    string outro = "\n\nThe team will get back to you shortly, hopefully this helps in the meantime!";
                    result = new IssueOutput
                    {
                        Category = resultObj.Category,
                        Service = resultObj.Service,
                        Suggestions = intro + resultObj.Response + outro,
                        Solution = solution
                    };
                }


                _logger.LogInformation($"Open AI Structured Response : \n{result}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to label issue:{ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return EmptyResult;
            }

            try
            {
                return new JsonResult(result);
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

        // Output given by the models.
        private class AIOutput
        {
            public string Category { get; set; }
            public string Service { get; set; }
            public string Response { get; set; }
        }
    }
}
