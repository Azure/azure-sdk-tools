// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Azure.Search.Documents.Models;
using OpenAI.Chat;
using System.Text.Json;
using Azure.Search.Documents.Indexes;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Azure.AI.OpenAI;
using System.Linq;

namespace IssueLabelerService
{
    public class TriageRag
    {
        private static AzureOpenAIClient s_openAiClient;
        private static SearchIndexClient s_searchIndexClient;
        private ILogger<TriageRag> _logger;

        public TriageRag(ILogger<TriageRag> logger, AzureOpenAIClient openAiClient, SearchIndexClient searchIndexClient)
        {
            s_openAiClient = openAiClient;
            _logger = logger;
            s_searchIndexClient = searchIndexClient;
        }


        public async Task<List<Content>> IssueTriageContentIndexAsync(
            string indexName,
            string semanticConfigName,
            string field,
            string query,
            int count,
            double scoreThreshold,
            Dictionary<string, string> labels = null)
        {
            var searchResults = await AzureSearchQueryAsync<Content>(
                indexName,
                semanticConfigName,
                field,
                query,
                count
            );

            List<Content> filteredIssues = new List<Content>();
            foreach (var (issue, score) in searchResults)
            {
                if (score >= scoreThreshold)
                {
                    issue.Score = score;
                    filteredIssues.Add(issue);
                }
            }
            return filteredIssues;
        }

        public double GetHighestScoreForContent(IEnumerable<Content> issues, string repositoryName, int issueNumber)
        {
          foreach (var issue in issues)
            {
                if (issue.Score == null)
                {
                    throw new Exception($"An issue in the search results for {repositoryName} using the Open AI Labeler for issue #{issueNumber} has a null score.");
                }
            }
            return issues.Max(issue => issue.Score ?? double.MinValue);  
        }
        
        public async Task<List<(T, double)>> AzureSearchQueryAsync<T>(
            string indexName,
            string semanticConfigName,
            string field,
            string query,
            int count,
            string filter = null)
        {
            SearchClient searchClient = s_searchIndexClient.GetSearchClient(indexName);

            _logger.LogInformation($"Searching for related {typeof(T).Name.ToLower()}s...");
            SearchOptions options = new SearchOptions
            {
                Size = count,
                QueryType = SearchQueryType.Semantic
            };

            options.VectorSearch = new()
            {
                Queries =
                {
                    new VectorizableTextQuery(text: query)
                    {
                        KNearestNeighborsCount = 50,
                        Fields = { field }
                    }
                }
            };

            options.SemanticSearch = new()
            {
                SemanticConfigurationName = semanticConfigName
            };

            options.Filter = filter;


            SearchResults<T> response = await searchClient.SearchAsync<T>(
                query,
                options);


            _logger.LogInformation($"{typeof(T).Name}s found.");

            List<(T, double)> results = new List<(T, double)>();
            foreach (SearchResult<T> result in response.GetResults())
            {
                results.Add((result.Document, result.SemanticSearch.RerankerScore ?? 0.0));
            }

            return results;
        }

        public async Task<string> SendMessageQnaAsync(string instructions, string message, string modelName, BinaryData structure = null)
        {
            ChatClient chatClient = s_openAiClient.GetChatClient(modelName);
            _logger.LogInformation($"\n\nWaiting for an Open AI response...");

            ChatCompletionOptions options = new ChatCompletionOptions();

            if (modelName.Contains("gpt"))
            {
                options.Temperature = 0;
            }

            if(modelName.Contains("o3-mini"))
            {
                options.ReasoningEffortLevel = ChatReasoningEffortLevel.Medium;
            }

            if(structure != null)
                options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "IssueOutput",
                    jsonSchema: structure
                );

            ChatCompletion answers = await chatClient.CompleteChatAsync(
                [
                    new DeveloperChatMessage(instructions),
                    new UserChatMessage(message)
                ],
                options
            );

            _logger.LogInformation($"\n\nFinished loading Open AI response.");

            return answers.Content[0].Text;
        }
    }
    public class Content
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string chunk { get; set; }
        public string Service { get; set; }
        public string Category { get; set; }
        public string Author { get; set; }
        public string Repository { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
        public string Url { get; set; }
        public double? Score { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
