using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using IssueLabeler.Shared;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Azure.Storage.Blobs;
using Newtonsoft.Json;

namespace IssueLabelerService
{
    public class OpenAiLabeler : ILabeler
    {
        private ILogger<LabelerFactory> _logger;
        private RepositoryConfiguration _config;
        private TriageRag _ragService;
        private BlobServiceClient _blobClient;

        public OpenAiLabeler(ILogger<LabelerFactory> logger, RepositoryConfiguration config, TriageRag ragService, BlobServiceClient blobClient) =>
            (_logger, _config, _ragService, _blobClient) = (logger, config, ragService, blobClient);

        public async Task<Dictionary<string, string>> PredictLabels(IssuePayload issue)
        {
            var modelName = _config.LabelModelName;
            var indexName = _config.IndexName;
            var semanticName = _config.SemanticName;
            string query = $"{issue.Title} {issue.Body}";
            int top = int.Parse(_config.SourceCount);
            double scoreThreshold = double.Parse(_config.ScoreThreshold);
            var fieldName = _config.IssueIndexFieldName;
            double solutionThreshold = double.Parse(_config.SolutionThreshold);

            _logger.LogInformation($"Searching content index '{indexName}' with query: {query}");
            var searchContentResults = await _ragService.IssueTriageContentIndexAsync(
                indexName,
                semanticName,
                fieldName,
                query,
                top,
                scoreThreshold
            );
            // If no results are found, throw an exception
            if (searchContentResults.Count == 0)
            {
                throw new Exception($"Not enough relevant sources found for {issue.RepositoryName} using the Complete Triage model for issue #{issue.IssueNumber}.");
            }
            _logger.LogInformation($"Found {searchContentResults.Count} issues with score >= {scoreThreshold}");



            //RAG service
            var labels = await GetLabelsAsync(issue.RepositoryName);
            var categoryLabels = GetCategoryLabelsForPrompt(labels, issue.RepositoryName);

            var printableContent = string.Join("\n\n", searchContentResults.Select(searchContent =>
                $"Title: {searchContent.Title}\nDescription: {searchContent.chunk}\nURL: {searchContent.Url}\nScore: {searchContent.Score}"));

            double highestScore = _ragService.GetHighestScoreForContent(searchContentResults, issue.RepositoryName, issue.IssueNumber);
            _logger.LogInformation($"Highest relevance score among the sources: {highestScore}");

            string instructions = _config.LabelInstructions;
            var replacements = new Dictionary<string, string>
            {
                { "Title", issue.Title },
                { "Description", issue.Body },
                { "PrintableLabels", categoryLabels },
                { "PrintableContent", printableContent }
            };
            string userPrompt = AzureSdkIssueLabelerService.FormatTemplate(_config.LabelPrompt, replacements, _logger);
            var structure = BuildSearchStructure();

            var result = await _ragService.SendMessageQnaAsync(instructions, userPrompt, modelName, structure);
            var output = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);

            if (string.IsNullOrEmpty(result))
            {
                throw new InvalidDataException($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} had an empty response.");
            }

            // Filter the output to exclude keys containing "ConfidenceScore"
            var filteredOutput = output
                .Where(kv => !kv.Key.Contains("ConfidenceScore", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if(!ValidateLabels(labels, filteredOutput))
            {
                throw new InvalidDataException($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} provided invalid labels: {string.Join(", ", filteredOutput.Select(kv => $"{kv.Key}: {kv.Value}"))}");
            }

            foreach (var label in filteredOutput)
            {
                try
                {
                    var confidence = double.Parse(output[$"{label.Key}ConfidenceScore"]);
                    if(label.Value == "UNKNOWN")
                    {
                        throw new InvalidDataException($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} provided an UNKNOWN label.");
                    }
                    if (confidence < double.Parse(_config.ConfidenceThreshold))
                    {
                        throw new InvalidDataException($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} Confidence below threshold: {confidence} < {_config.ConfidenceThreshold}.");
                    }
                }
                catch(FormatException)
                {
                    throw new InvalidDataException($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber} provided an invalid confidence scoreor config score threshold not setup: {output[$"{label.Key}ConfidenceScore"]}");
                }
            }

            _logger.LogInformation($"Open AI Response for {issue.RepositoryName} using the Open AI Labeler for issue #{issue.IssueNumber}: {string.Join(", ", filteredOutput.Select(kv => $"{kv.Key}: {kv.Value}"))}");

            return filteredOutput;
        }

        private BinaryData BuildSearchStructure()
        {
            // Dynamically generate the JSON schema based on the configured labels
            var labelKeys = _config.LabelNames.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var properties = string.Join(", ", labelKeys.Select(key => $"\"{key}\": {{ \"type\": \"string\" }}"));
            var scores = string.Join(", ", labelKeys.Select(key => $"\"{key}ConfidenceScore\": {{ \"type\": \"string\" }}"));
            var required = string.Join(", ", labelKeys.Select(key => $"\"{key}\""));
            return BinaryData.FromString($$"""
            {
              "type": "object",
              "properties": {
                {{properties}},{{scores}}
              },
              "required": [ {{required}} ],
              "additionalProperties": false
            }
            """);
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

        private string GetCategoryLabelsForPrompt(IEnumerable<Label> labels, string repositoryName)
        {
            // Filter for category labels (color = e99695)
            var categoryLabels = labels
                .Where(label => string.Equals(label.Color, "e99695", StringComparison.OrdinalIgnoreCase))
                .Select(label => label.Name)
                .ToList();

            return "['" + string.Join("', '", categoryLabels) + "'']";
        }

        private bool ValidateLabels(IEnumerable<Label> labels, Dictionary<string, string> labelsToValidate)
        {
            // Get all label names
            var labelNames = labels.Select(label => label.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Validate that all values in the dictionary exist in the label names
            return labelsToValidate.Values.All(label => labelNames.Contains(label));
        }
    }
}
