using Azure.Search.Documents;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents.Models;
using OpenAI.Embeddings;
using Microsoft.Extensions.Configuration;

namespace IssueLabelerAzureSearch
{
    public class IssueLabelerAzureSearch
    {
        private Uri _endpoint;
        private AzureKeyCredential _credential;
        private string _indexName;

        public IssueLabelerAzureSearch(Uri endpoint, AzureKeyCredential credential, string indexName)
        {
            _endpoint = endpoint;
            _credential = credential;
            _indexName = indexName;
        }

        public IEnumerable<string> AzureSearchQuery(string query, int count)
        {
            SearchClient searchClient = new SearchClient(_endpoint, _indexName, _credential);

            string embeddingModel = "";
            ReadOnlyMemory<float> vectorizedResult = Vectorize(query, embeddingModel);

            SearchOptions options = new SearchOptions
            {
                Size = count,
                VectorSearch = new()
                {
                    Queries = { new VectorizedQuery(vectorizedResult) { KNearestNeighborsCount = count, Fields = { "Body_Vector", "Comments_Vector" } } }
                }
            };

            SearchResults<Issue> response = searchClient.Search<Issue>(
                query,
                options);

            foreach (SearchResult<Issue> result in response.GetResults())
            {
                yield return ($"{{ title: \"{result.Document.Title}\", description: \"{result.Document.Body}\", comment: \"{result.Document.Comments}\", service: \"{result.Document.Service}\", category: \"{result.Document.Category}\", author: \"{result.Document.Author}\"}}");
            }
        }

        public void AddToIndex(List<IssueRetrieval.Issue> issues)
        {
            try
            {
                for (int i = 0; i < issues.Count; i += 50)
                {
                    int end = i + 50;
                    if(end > issues.Count)
                    {
                        end = issues.Count;
                    }

                    SearchClient searchClient = new SearchClient(_endpoint, _indexName, _credential);
                    IndexDocumentsBatch<IssueRetrieval.Issue> batch = IndexDocumentsBatch.Create(
                    issues.GetRange(i, end).Select(item => IndexDocumentsAction.Upload(item)).ToArray());

                    IndexDocumentsOptions options = new IndexDocumentsOptions { ThrowOnAnyError = true };
                    searchClient.IndexDocuments(batch, options);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to index documents: {e.Message}");
            }
        }   

        private ReadOnlyMemory<float> Vectorize(string input, string embeddingModel)
        {
            AzureOpenAIClient openAIClient = new AzureOpenAIClient(_endpoint, _credential);
            EmbeddingClient embeddingClient = openAIClient.GetEmbeddingClient(embeddingModel);

            OpenAIEmbedding embedding = embeddingClient.GenerateEmbedding(input);
            return embedding.ToFloats();
        }

        public class Issue
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Body { get; set; }
            //public ReadOnlyMemory<float> Body_Vector { get; set; }
            public string Body_Vector { get; set; }
            public string Comments { get; set; }
            //public ReadOnlyMemory<float> Comments_Vector { get; set; }
            public String Comments_Vector { get; set; }
            public string Service { get; set; }
            public string Category { get; set; }
            public string Author { get; set; }
            public string Repository { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
        }
    }
}
