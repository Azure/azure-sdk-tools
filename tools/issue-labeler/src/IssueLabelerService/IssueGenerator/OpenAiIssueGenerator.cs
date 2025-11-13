using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Search.Documents.Agents;
using Azure.Search.Documents.Agents.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IssueLabeler.Shared;

namespace IssueLabelerService
{
    public class OpenAiIssueGenerator : IIssueGeneratorService
    {
        private RepositoryConfiguration _config;
        private TriageRag _ragService;
        private ILogger<IssueGeneratorFactory> _logger;
        private readonly KnowledgeAgentRetrievalClient _retrievalClient;
        private BlobServiceClient _blobClient;

        public OpenAiIssueGenerator(ILogger<IssueGeneratorFactory> logger, RepositoryConfiguration config, TriageRag ragService, BlobServiceClient blobClient)
        {
            _config = config;
            _ragService = ragService;
            _logger = logger;
            _blobClient = blobClient;
            _retrievalClient = new KnowledgeAgentRetrievalClient(
                endpoint: new Uri(_config.SearchEndpoint),
                agentName: _config.KnowledgeAgentName,
                tokenCredential: new DefaultAzureCredential()
            );
        }
        public async Task<string> GenerateIssue(string repositoryName)
        {
            var modelName = _config.AnswerModelName;
            var instructions = _config.IssueGeneratorInstruction;
            var repoLabels = await GetLabelsAsync(repositoryName);
            var applicableServiceLabels = GetServiceLabelsForPrompt(repoLabels);
            var applicableCategoryLabels = GetCategoryLabelsForPrompt(repoLabels);
            var replacements = new Dictionary<string, string>
            {
                { "repositoryName", repositoryName },
                { "numIssues", "50"},
                { "applicableServiceLabels", applicableServiceLabels},
                { "applicableCategoryLabels", applicableCategoryLabels}
            };
            var message = AzureSdkIssueLabelerService.FormatTemplate(_config.IssueGeneratorMessage, replacements, _logger);

            KnowledgeAgentRetrievalRequest retrievalRequest = BuildRetrievalRequest(instructions, message);

            var retrievalResult = await _retrievalClient.RetrieveAsync(retrievalRequest);

            var contextBlock = GetContextBlock(retrievalResult);

            var response = await _ragService.SendMessageQnaAsync(instructions, message, modelName, contextBlock);

            // var formattedResponse = FormatResponse(SuggestionsAnswerType, issue, response);

            // _logger.LogInformation($"Open AI generated issue for {repositoryName} using the Complete Triage model: \n{response}");
            var issues = JsonConvert.DeserializeObject<IEnumerable<IssueTriageContent>>(response);

            // Write answer to JSON file - answerData is an array of objects
            var jsonFileName = $"issue_answer_{repositoryName}_3.json";
            var fileName = $"issue_answer_{repositoryName}_3.tsv";
            using StreamWriter writer = new StreamWriter(fileName);
            writer.WriteLine(FormatIssueRecord("CategoryLabel", "ServiceLabel", "Title", "Body"));
            foreach (var issue in issues)
            {
                if (issue.Category is null || issue.Service is null || issue.Title is null || issue.Body is null
                || !applicableCategoryLabels.Contains(issue.Category) || !applicableServiceLabels.Contains(issue.Service)) continue;

                writer.WriteLine(FormatIssueRecord(issue.Category, issue.Service, issue.Title, issue.Body));
            }
            await File.WriteAllTextAsync(jsonFileName, response);
            _logger.LogInformation($"Answer written to file: {fileName}");
            return response;
        }

        private KnowledgeAgentRetrievalRequest BuildRetrievalRequest(string instructions, string message)
        {
            var agentMessages = new[]
            {
                new KnowledgeAgentMessage("assistant", new KnowledgeAgentMessageContent[] { new KnowledgeAgentMessageTextContent(instructions) }),
                new KnowledgeAgentMessage("user", new KnowledgeAgentMessageContent[] { new KnowledgeAgentMessageTextContent(message) }),
            };


            var retrievalRequest = new KnowledgeAgentRetrievalRequest(agentMessages)
            {
                TargetIndexParams =
                {
                    new KnowledgeAgentIndexParams
                    {
                        IndexName = _config.IndexName,
                        RerankerThreshold = 1.0f,
                        MaxDocsForReranker = 200,
                    }
                }
            };
            return retrievalRequest;
        }

        private string GetContextBlock(KnowledgeAgentRetrievalResponse retrievalResult)
        {
            if (retrievalResult.Response.Count == 0)
            {
                return null;
            }

            var snippets = retrievalResult.Response[0].Content
                .OfType<KnowledgeAgentMessageTextContent>()
                .Select(content => content.Text)
                .ToList();

            var allSources = new List<JObject>();

            foreach (var snippet in snippets)
            {
                try
                {
                    var arr = JArray.Parse(snippet);
                    foreach (var obj in arr)
                    {
                        if (obj is JObject jobj)
                            allSources.Add(jobj);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to parse snippet as JSON array: {ex.Message}");
                }
            }

            _logger.LogInformation($"Number of sources retrieved: {allSources.Count}");

            return string.Join(
                "\n\n",
                allSources.Select((obj, i) =>
                    $"Title: {obj["title"]}\nDescription: {obj["content"]}\nTerms: {obj["terms"]}")
            );
        }
        private async Task<IEnumerable<Label>> GetLabelsAsync(string repositoryName)
        {
            // Initialize BlobServiceClient
            var containerClient = _blobClient.GetBlobContainerClient("labels");

            // Get the blob client for the specific repository
            var blobClient = containerClient.GetBlobClient(repositoryName);

            // Check if the blob exists
            if (!await blobClient.ExistsAsync())
            {
                throw new FileNotFoundException($"Blob for repository '{repositoryName}' not found.");
            }

            // Download the blob content
            var response = await blobClient.DownloadContentAsync();
            var labelsJson = response.Value.Content.ToString();

            // Deserialize the JSON into a list of labels
            var labels = JsonConvert.DeserializeObject<IEnumerable<Label>>(labelsJson);

            if (labels == null)
            {
                throw new InvalidOperationException("Failed to deserialize labels from blob.");
            }

            return labels;
        }

        private string GetCategoryLabelsForPrompt(IEnumerable<Label> labels)
        {
            var categoryLabels = labels
                .Where(AzureSdkLabel.IsCategoryLabel)
                .Select(label => label.Name)
                .ToList();

            return string.Join(", ", categoryLabels);
        }
        private string GetServiceLabelsForPrompt(IEnumerable<Label> labels)
        {
            var serviceLabels = labels
                .Where(AzureSdkLabel.IsServiceLabel)
                .Select(label => label.Name)
                .ToList();

            return string.Join(", ", serviceLabels);
        }

                public static string FormatTemplate(string template, Dictionary<string, string> replacements, ILogger logger)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            string result = template;

            foreach (var replacement in replacements)
            {
                if (!result.Contains($"{{{replacement.Key}}}"))
                {
                    logger.LogWarning($"Replacement value for {replacement.Key} does not exist in {template}.");
                }
                result = result.Replace($"{{{replacement.Key}}}", replacement.Value);
            }

            // Replace escaped newlines with actual newlines
            return result.Replace("\\n", "\n");
        }

        public static string FormatIssueRecord(string categoryLabel, string serviceLabel, string title, string body)
        => string.Join('\t',
        [
            SanitizeText(categoryLabel),
            SanitizeText(serviceLabel),
            SanitizeText(title),
            SanitizeText(body)
        ]);
        
        public static string SanitizeText(string text)
        => text
        .Replace('\r', ' ')
        .Replace('\n', ' ')
        .Replace('\t', ' ')
        .Replace('"', '`')
        .Trim();
    }
}
