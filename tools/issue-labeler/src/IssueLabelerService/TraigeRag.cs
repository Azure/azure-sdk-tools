// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Azure.Search.Documents.Models;
using OpenAI.Chat;
using System.Text.Json;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace IssueLabelerService
{
    public class TriageRag
    {
        private static ChatClient s_chatClient;
        private static SearchIndexClient s_searchIndexClient;
        private ILogger<TriageRag> _logger;

        public TriageRag(ILogger<TriageRag> logger, ChatClient chatClient, SearchIndexClient searchIndexClient)
        {
            s_chatClient = chatClient;
            _logger = logger;
            s_searchIndexClient = searchIndexClient;
        }

        public async Task<List<(T, double)>> AzureSearchQueryAsync<T>(
            string indexName,
            string semanticConfigName,
            string field,
            string query,
            int count)
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

            SearchResults<T> response = await searchClient.SearchAsync<T>(
                query,
                options);


            _logger.LogInformation($"{typeof(T).Name}s found.");

            List<(T, double)> results = new List<(T, double)>();
            foreach (SearchResult<T> result in response.GetResults())
            {
                _logger.LogInformation(result.SemanticSearch.RerankerScore.ToString());
                results.Add((result.Document, result.SemanticSearch.RerankerScore ?? 0.0));
            }

            return results;
        }

        public async Task<string> SendMessageQnaAsync(string instructions, string message, BinaryData structure = null)
        {

            _logger.LogInformation($"\n\nWaiting for an Open AI response...");

            ChatCompletionOptions options = new ChatCompletionOptions()
            {
                ReasoningEffortLevel = ChatReasoningEffortLevel.Medium,
            };

            if(structure != null)
                options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "IssueOutput",
                    jsonSchema: structure
                );

            ChatCompletion answers = await s_chatClient.CompleteChatAsync(
                [
                    new DeveloperChatMessage(instructions),
                    new UserChatMessage(message)
                ],
                options
            );

            _logger.LogInformation($"\n\nFinished loading Open AI response.");

            return answers.Content[0].Text;
        }

        public async Task<List<Document>> SearchDocumentsAsync(
            string indexName,
            string semanticConfigName,
            string field,
            string query,
            int count,
            double scoreThreshold)
        {
            var searchResults = await AzureSearchQueryAsync<Document>(
                indexName,
                semanticConfigName,
                field,
                query,
                count);

            List<Document> filteredDocuments = new List<Document>();
            foreach (var (document, score) in searchResults)
            {
                if (score >= scoreThreshold)
                {
                    document.Score = score;
                    filteredDocuments.Add(document);
                }
            }

            _logger.LogInformation($"Found {filteredDocuments.Count} documents with score >= {scoreThreshold}");
            return filteredDocuments;
        }

        public async Task<List<Issue>> SearchIssuesAsync(
            string indexName,
            string semanticConfigName,
            string field,
            string query,
            int count,
            double scoreThreshold)
        {
            var searchResults = await AzureSearchQueryAsync<Issue>(
                indexName,
                semanticConfigName,
                field,
                query,
                count);

            List<Issue> filteredIssues = new List<Issue>();
            foreach (var (issue, score) in searchResults)
            {
                if (score >= scoreThreshold)
                {
                    issue.Score = score;
                    filteredIssues.Add(issue);
                }
            }

            _logger.LogInformation($"Found {filteredIssues.Count} issues with score >= {scoreThreshold}");
            return filteredIssues;
        }

        public double GetHighestScore(IEnumerable<Issue> issues, IEnumerable<Document> docs, string repositoryName, int issueNumber)
        {
            double highestScore = double.MinValue;

            // Check scores in docs
            foreach (var doc in docs)
            {
                if (doc.Score == null)
                {
                    throw new Exception($"A document in the search results for {repositoryName} using the Open AI Labeler for issue #{issueNumber} has a null score.");
                }
                if (doc.Score > highestScore)
                {
                    highestScore = doc.Score.Value;
                }
            }

            // Check scores in issues
            foreach (var issue in issues)
            {
                if (issue.Score == null)
                {
                    throw new Exception($"An issue in the search results for {repositoryName} using the Open AI Labeler for issue #{issueNumber} has a null score.");
                }
                if (issue.Score > highestScore)
                {
                    highestScore = issue.Score.Value;
                }
            }

            return highestScore;
        }
    }

    public class Issue
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string chunk { get; set; }
        public string Service { get; set; }
        public string Category { get; set; }
        public string Author { get; set; }
        public string Repository { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string Url { get; set; }
        public double? Score { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class Document
    {
        public string chunk { get; set; }
        public string Url { get; set; }
        public double? Score { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
