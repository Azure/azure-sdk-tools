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
using IssueManager;
using Azure.Identity;
using System.Collections.Generic;

namespace IssueLabelerService
{
    public class AzureSdkIssueLabelerService
    {
        private static readonly ActionResult EmptyResult = new JsonResult(Array.Empty<string>());
        private readonly ILogger<AzureSdkIssueLabelerService> _logger;
        private IConfiguration Config { get; }
        private IIssueLabelerAzureSearch _issueLabeler { get; }

        public AzureSdkIssueLabelerService(IConfiguration config, ILogger<AzureSdkIssueLabelerService> logger, IIssueLabelerAzureSearch issueLabeler)
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
                // Configurations for correct access in search and open ai
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

                string query = JsonConvert.SerializeObject(issue);

                //Top x documents/issues
                int top = 5;

                // Semantic score from 0 - 4, 4 being very relevant
                double scoreThreshold = 2.0;

                // OpenAI token threshold around 130k for input (as of writing this)
                // limit to around 100k to give space for prompt + query
                double tokenThreshold = 100000;

                IEnumerable<(Issue, double)> relevantIssues = _issueLabeler.AzureSearchQuery<Issue>(searchEndpoint, issueIndexName, issueSemanticName, issueFieldName, credential, query, top);

                IEnumerable<(Document, double)> relevantDocuments = _issueLabeler.AzureSearchQuery<Document>(searchEndpoint, documentIndexName, documentSemanticName, documentFieldName, credential, query, top);

                string content = await _issueLabeler.FilterAndCombine(relevantIssues, relevantDocuments, scoreThreshold, tokenThreshold);

                string json_format = "{\n\"service\": \"\",\n\"category\": \"\",\n\"suggestions\": \"\"\n}";
                //string message = "You are an AI Assistant that helps users learn from the information found in the material. Answer the query using only the sources provided below." +
                //    "The query represents a github issue. Your job is to provide a service label, category label, and offer the user suggestions on how to solve there issue." +
                //    "service and category must come from the labels provided in the sources, the sources are a JSON array of previous issues that include service and category labels. " +
                //    //"If there isn't enough information below do not answer. " +
                //    "Do not generate answers that don't use the sources below. " +
                //    $"Return ONLY in the following format and nothing else: {json_format} " +
                //    $"Query: {query} " +
                //    $"Sources: {content}";

                string message = $"You are an AI assistant that helps users with issues based on the provided material. Below is a query and relevant sources (GitHub issues and documentation). Your role is to:\n\nIdentify the most appropriate service and category labels from the sources provided.\nProvide actionable suggestions based on the sources to help with the query.\nReference URLs from the sources for each suggestion to back up your recommendations.\nGuidelines:\nUse only the information from the sources below to construct your response.\nInclude URLs to specific sources where the information or suggestions originate.\nIf you cannot determine a solution due to insufficient information, state that explicitly.\nEnsure your response is structured strictly in the format defined below:\n{json_format}\nQuery: {query}\nSources: {content}\nNotes:\nDo not speculate or provide information that is not explicitly supported by the sources.";

                result = _issueLabeler.SendMessageQna(openAIEndpoint, credential, modelName, message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to label issue:{ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return new BadRequestResult();
            }

            try
            {
                return new JsonResult(JsonConvert.DeserializeObject<IssueOutput>(result));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to deserialize output:{ex.Message}{Environment.NewLine}\t{ex}{Environment.NewLine}");
                return new BadRequestResult();
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
        }

    }
}
