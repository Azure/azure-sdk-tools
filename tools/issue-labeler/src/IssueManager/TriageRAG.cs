// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Azure.AI.OpenAI;
using Azure.Search.Documents.Models;
using OpenAI.Chat;
using Azure.Identity;
using System.Text.Json;

namespace AzureRAGService
{
    public interface ITriageRAG
    {
        IEnumerable<(T, double)> AzureSearchQuery<T>(Uri searchEndpoint, string indexName, string semanticConfigName, string field, DefaultAzureCredential credential, string query, int count);
        string SendMessageQna(Uri openAIEndpoint, DefaultAzureCredential credential, string modelName, string instructions, string message, BinaryData structure);
        string StructureMessage(Uri openAIEndpoint, DefaultAzureCredential credential, string modelName, string message, BinaryData structure);
    }

    public class TriageRAG : ITriageRAG
    {
        private ILogger<ITriageRAG> _logger;

        public TriageRAG(ILogger<ITriageRAG> logger)
        {
            _logger = logger;
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
        public IEnumerable<(T, double)> AzureSearchQuery<T>(
            Uri searchEndpoint,
            string indexName,
            string semanticConfigName,
            string field,
            DefaultAzureCredential credential,
            string query,
            int count)
        {
            SearchClient searchClient = new SearchClient(searchEndpoint, indexName, credential);

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

            SearchResults<T> response = searchClient.Search<T>(
                query,
                options);

            _logger.LogInformation($"{typeof(T).Name}s found.");

            foreach (SearchResult<T> result in response.GetResults())
            {
                _logger.LogInformation(result.SemanticSearch.RerankerScore.ToString());
                yield return (result.Document, result.SemanticSearch.RerankerScore ?? 0.0);
            }
        }

        /// <summary>
        /// Sends a message to the OpenAI QnA model. Message must include both the prompt and the query
        /// </summary>
        /// <param name="openAIEndpoint">The OpenAI endpoint URI.</param>
        /// <param name="credential">The Azure credential.</param>
        /// <param name="modelName">The name of the OpenAI model.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>The response from the OpenAI model.</returns>
        public string SendMessageQna(Uri openAIEndpoint, DefaultAzureCredential credential, string modelName, string instructions, string message, BinaryData structure)
        {
            AzureOpenAIClient openAIClient = new(openAIEndpoint, credential);
            ChatClient chatClient = openAIClient.GetChatClient(modelName);

            _logger.LogInformation($"\n\nWaiting for an Open AI response...");

            ChatCompletionOptions options = new ChatCompletionOptions()
            {
                ReasoningEffortLevel = ChatReasoningEffortLevel.High,
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "IssueOutput",
                    jsonSchema: structure
                )
            };

            ChatCompletion answers = chatClient.CompleteChat(
                [
                    new DeveloperChatMessage(instructions),
                    new UserChatMessage(message)
                ],
                options
            );

            _logger.LogInformation($"\n\nFinished loading Open AI response.");

            return answers.Content[0].Text;
        }

        /// <summary>
        /// Structures a message into the given JSON using the gpt4o model.
        /// </summary>
        /// <param name="openAIEndpoint">The OpenAI endpoint URI.</param>
        /// <param name="credential">The Azure credential.</param>
        /// <param name="modelName">The name of the OpenAI model.</param>
        /// <param name="message">The message to structure.</param>
        /// <param name="structure">The structure to apply to the message.</param>
        /// <returns>The structured message.</returns>
        public string StructureMessage(Uri openAIEndpoint, DefaultAzureCredential credential, string modelName, string message, BinaryData structure)
        {
            AzureOpenAIClient openAIClient = new(openAIEndpoint, credential);
            ChatClient chatClient = openAIClient.GetChatClient(modelName);

            _logger.LogInformation($"\n\nStructuring OpenAI Response...");
            ChatClient chatClientStructure = openAIClient.GetChatClient("gpt-4o");

            ChatCompletionOptions options = new ChatCompletionOptions()
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "IssueOutput",
                    jsonSchema: structure
                    )
            };
            ChatCompletion structuredAnswer = chatClientStructure.CompleteChat(
                [
                    new UserChatMessage($"Given the following data: {message}\nFormat it accordingly without removing or changing the information in any way.")
                ],
                options
             );

            return structuredAnswer.Content[0].Text;
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