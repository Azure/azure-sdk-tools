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
        

        /*
        private string FormatResponse(string answerType, IssuePayload issue, string response)
        {
            string intro;
            string outro;

            var replacementsIntro = new Dictionary<string, string>
            {
            { "IssueUserLogin", issue.IssueUserLogin },
            { "RepositoryName", issue.RepositoryName }
            };

            if (answerType == SolutionAnswerType)
            {
            intro = AzureSdkIssueLabelerService.FormatTemplate(_config.SolutionResponseIntroduction, replacementsIntro, _logger);
            outro = _config.SolutionResponseConclusion;
            }
            else
            {
            intro = AzureSdkIssueLabelerService.FormatTemplate(_config.SuggestionResponseIntroduction, replacementsIntro, _logger);
            outro = _config.SuggestionResponseConclusion;
            }

            if (string.IsNullOrEmpty(response))
            {
            throw new Exception($"Open AI Response for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber} had an empty response.");
            }

            return intro + response + outro;
        }
        */
    }
}
