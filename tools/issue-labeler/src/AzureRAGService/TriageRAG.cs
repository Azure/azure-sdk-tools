// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Azure.Search.Documents.Models;
using OpenAI.Chat;
using Azure.Identity;
using System.Text.Json;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;

namespace AzureRagService
{
    public class TriageRag
    {
        private static ChatClient s_chatClient;
        private static SearchIndexClient s_searchIndexClient;
        private static List<SearchClient> s_searchClients = new List<SearchClient>();
        private ILogger<TriageRag> _logger;
        private IConfiguration _config;
        private List<String> indexNameSuffix = new List<string>
        {
            "Python",
            "DotNet"
        };

        public TriageRag(ILogger<TriageRag> logger, ChatClient chatClient, SearchIndexClient searchIndexClient, IConfiguration config)
        {
            s_chatClient = chatClient;
            _logger = logger;
            s_searchIndexClient = searchIndexClient;
            _config = config;

            LoadSearchClient();
        }

        /// <summary>
        /// Executes an Azure Search query.
        /// </summary>
        /// <typeparam name="T">The type of the search result.</typeparam>
        /// <param name="searchEndpoint">The search endpoint URI.</param>
        /// <param name="indexName">The name of the search index.</param>
        /// <param name="semanticConfigName">The name of the semantic configuration.</param>
        /// <param name="field">The field to search.</param>
        /// <param name="credential">The Azure credential.</param>
        /// <param name="query">The search query.</param>
        /// <param name="count">The number of results to return.</param>
        /// <returns>An enumerable of "search results" with their associated scores.</returns>
        public async Task<List<(T, double)>> AzureSearchQueryAsync<T>(
            Uri searchEndpoint,
            string indexName,
            string semanticConfigName,
            string field,
            DefaultAzureCredential credential,
            string query,
            int count)
        {
            if (s_searchClients.Count == 0)
            {
                LoadSearchClient();
            }

            SearchClient? searchClient = FindSearchClient(indexName);
            if (searchClient == null)
            {
                _logger.LogError($"SearchClient for index '{indexName}' not loaded.");
                return new List<(T, double)>();
            }

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

        /// <summary>
        /// Sends a message to the OpenAI model for Question and Answer.
        /// </summary>
        /// <param name="instructions">The developer instructions for the OpenAI model.</param>
        /// <param name="message">The message or user query to send.</param>
        /// <param name="structure">The JSON schema structure for the response.</param>
        /// <returns>The response from the OpenAI model.</returns>
        public async Task<string> SendMessageQnaAsync(string instructions, string message, BinaryData structure)
        {

            _logger.LogInformation($"\n\nWaiting for an Open AI response...");

            ChatCompletionOptions options = new ChatCompletionOptions()
            {
                ReasoningEffortLevel = ChatReasoningEffortLevel.Medium,
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "IssueOutput",
                    jsonSchema: structure
                )
            };

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

        private void LoadSearchClient()
        { 
            foreach (var indexSuffix in indexNameSuffix)
            {
                var docIndexName = $"DocumentIndexName{indexSuffix}";
                var docSearchClient = s_searchIndexClient.GetSearchClient(_config[docIndexName]);

                var issueIndexName = $"IssueIndexName{indexSuffix}";
                var issueSearchClient = s_searchIndexClient.GetSearchClient(_config[issueIndexName]);

                s_searchClients.Add(docSearchClient);
                s_searchClients.Add(issueSearchClient);
            }
        }

        /// <summary>
        /// Finds a SearchClient from the list of search clients given an index name.
        /// </summary>
        /// <param name="indexName">The name of the search index.</param>
        /// <returns>The SearchClient associated with the given index name, or null if not found.</returns>
        private SearchClient? FindSearchClient(string indexName)
        {
            foreach (var searchClient in s_searchClients)
            {
                if (searchClient.IndexName.Equals(indexName, StringComparison.OrdinalIgnoreCase))
                {
                    return searchClient;
                }
            }

            return null;
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

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class Document
    {
        public string chunk { get; set; }
        public string Url { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
