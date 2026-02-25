// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace IssueLabelerService
{
    public class TriageRag
    {
        private readonly OpenAIClient OpenAiClient;
        private readonly SearchIndexClient SearchIndexClient;
        private readonly ILogger<TriageRag> Logger;
        
        public TriageRag(ILogger<TriageRag> logger, OpenAIClient openAiClient, SearchIndexClient searchIndexClient)
        {
            OpenAiClient = openAiClient;
            Logger = logger;
            SearchIndexClient = searchIndexClient;
        }

        public async Task<List<IndexContent>> IssueTriageContentIndexAsync(
            string indexName,
            string semanticConfigName,
            string field,
            string query,
            int count,
            double scoreThreshold,
            string filter = null)
        {

            var searchResults = await AzureSearchQueryAsync<IndexContent>(
                indexName,
                semanticConfigName,
                field,
                query,
                count,
                filter
            );

            var filteredIssues = new List<IndexContent>();

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

        public async Task<List<(T, double)>> AzureSearchQueryAsync<T>(
            string indexName,
            string semanticConfigName,
            string field,
            string query,
            int count,
            string filter = null)
        {
            SearchClient searchClient = SearchIndexClient.GetSearchClient(indexName);

            Logger.LogInformation($"Searching for related {typeof(T).Name.ToLower()}s...");
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

            Logger.LogInformation($"{typeof(T).Name}s found.");

            List<(T, double)> results = new List<(T, double)>();
            foreach (SearchResult<T> result in response.GetResults())
            {
                results.Add((result.Document, result.SemanticSearch.RerankerScore ?? 0.0));
            }

            return results;
        }

        public async Task<string> SendMessageQnaAsync(string instructions, string message, string modelName, string contextBlock = null, BinaryData structure = null)
        {
            Logger.LogInformation($"\n\nWaiting for an Open AI response...");
            ChatClient chatClient = OpenAiClient.GetChatClient(modelName);

            ChatCompletionOptions options = new ChatCompletionOptions();

            if (modelName.Contains("gpt"))
            {
                options.Temperature = 0;
            }

            if (modelName.Contains("o3-mini"))
            {
                options.ReasoningEffortLevel = ChatReasoningEffortLevel.Medium;
            }

            if (structure != null)
            {
                options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "IssueOutput",
                    jsonSchema: structure
                );
            }

            var chatMessages = new List<ChatMessage>
            {
                new DeveloperChatMessage(instructions),
                new UserChatMessage(message)
            };

            if (contextBlock != null)
            {
                chatMessages.Add(new AssistantChatMessage(contextBlock));
            }

            ChatCompletion result = await chatClient.CompleteChatAsync(chatMessages, options);

            Logger.LogInformation($"\n\nFinished loading Open AI response.");

            return result.Content[0].Text;
        }
    }
}
