using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Azure.AI.OpenAI;
using Azure.Search.Documents.Models;
using OpenAI.Embeddings;
using OpenAI.Chat;
using Azure.Identity;
using System.Text.Json;
using Microsoft.DeepDev;

namespace IssueManager
{
    public interface IIssueLabelerAzureSearch
    {
        IEnumerable<(T, double)> AzureSearchQuery<T>(Uri searchEndpoint, string indexName, string semanticConfigName, string field, DefaultAzureCredential credential, string query, int count);
        string SendMessageQna(Uri openAIEndpoint, DefaultAzureCredential credential, string modelName, string message);
        Task<string> FilterAndCombine(IEnumerable<(Issue issue, double score)> relevantIssues, IEnumerable<(Document document, double score)> relevantDocuments, double scoreThreshold, double tokenThreshold);
    }

    public class IssueLabelerAzureSearch : IIssueLabelerAzureSearch
    {
        private ILogger<IIssueLabelerAzureSearch> _logger;

        public IssueLabelerAzureSearch(ILogger<IIssueLabelerAzureSearch> logger)
        {
            _logger = logger;
        }

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

        //Message must include both the prompt and the query
        public string SendMessageQna(Uri openAIEndpoint, DefaultAzureCredential credential, string modelName, string message)
        {
            AzureOpenAIClient openAIClient = new(openAIEndpoint, credential);
            ChatClient chatClient = openAIClient.GetChatClient(modelName);

            _logger.LogInformation($"\n\nWaiting for an Open AI response...");


            ChatCompletion answers =
                chatClient.CompleteChat(
                    [
                    //Apparently not supported on o1 models
                    //new SystemChatMessage(prompt),
                    new UserChatMessage(message),
                    ]);

            _logger.LogInformation($"\n\nStructuring OpenAI Response...");
            ChatClient chatClientStructure = openAIClient.GetChatClient("gpt-4o");

            ChatCompletionOptions options = new ChatCompletionOptions()
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "IssueOutput",
                    jsonSchema: BinaryData.FromBytes("""
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
                        """u8.ToArray())
                    )
            };
            ChatCompletion structuredAnswer = chatClientStructure.CompleteChat(
                [
                    new UserChatMessage($"Given the following data, format it with the given response format: {answers.Content[0].Text}")
                ],
                options
             );

            //_logger.LogInformation($"Open AI Response : \n {answers.Content[0].Text}");

            return structuredAnswer.Content[0].Text;
        }

        private ReadOnlyMemory<float> Vectorize(Uri openAIEndpoint, DefaultAzureCredential credential, string input, string embeddingModel)
        {
            AzureOpenAIClient openAIClient = new AzureOpenAIClient(openAIEndpoint, credential);
            EmbeddingClient embeddingClient = openAIClient.GetEmbeddingClient(embeddingModel);

            OpenAIEmbedding embedding = embeddingClient.GenerateEmbedding(input);
            return embedding.ToFloats();
        }

        private async Task<int> GetTokenLength(string text)
        {
            var tokenizer = await TokenizerBuilder.CreateByModelNameAsync("gpt-4");
            IList<int> tokens = tokenizer.Encode(text, allowedSpecial: null);
            return tokens.Count;
        }

        public async Task<string> FilterAndCombine(
            IEnumerable<(Issue issue, double score)> relevantIssues,
            IEnumerable<(Document document, double score)> relevantDocuments,
            double scoreThreshold,
            double tokenThreshold)
        {
            // Filter issues and documents based on the threshold
            var filteredIssues = relevantIssues.Where(item => item.score >= scoreThreshold).ToList();
            var filteredDocuments = relevantDocuments.Where(item => item.score >= scoreThreshold).ToList();

            // Build a string representation of the remaining elements
            var resultBuilder = new System.Text.StringBuilder();
            int currentTokenCount = 0;

            // Helper function to add content and check token limits
            async Task<bool> TryAddContent(string content)
            {
                int newTokenCount = await GetTokenLength(resultBuilder.ToString() + content);
                if (newTokenCount <= tokenThreshold)
                {
                    resultBuilder.AppendLine(content);
                    currentTokenCount = newTokenCount;
                    return true;
                }
                return false;
            }

            // Initialize iterators for documents and issues
            var documentEnumerator = filteredDocuments.GetEnumerator();
            var issueEnumerator = filteredIssues.GetEnumerator();
            bool hasMoreDocuments = documentEnumerator.MoveNext();
            bool hasMoreIssues = issueEnumerator.MoveNext();

            while (hasMoreDocuments || hasMoreIssues)
            {
                // Add one document if available and within token limit
                if (hasMoreDocuments)
                {
                    var documentContent = documentEnumerator.Current.document.ToString();
                    if (!await TryAddContent(documentContent))
                    {
                        break; // Stop adding if token threshold is exceeded
                    }
                    hasMoreDocuments = documentEnumerator.MoveNext();
                }

                // Add one issue if available and within token limit
                if (hasMoreIssues)
                {
                    var issueContent = issueEnumerator.Current.issue.ToString();
                    if (!await TryAddContent(issueContent))
                    {
                        break; // Stop adding if token threshold is exceeded
                    }
                    hasMoreIssues = issueEnumerator.MoveNext();
                }
            }

            return resultBuilder.ToString();
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
